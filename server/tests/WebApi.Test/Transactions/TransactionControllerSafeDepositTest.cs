using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;
using Xunit;

namespace WebApi.Test.Transactions;

/// <summary>
/// M9.6 Phase 9 (§6.19 O09, E08): <i>Depósito Dinheiro</i> — cashbox Out on its session and the generated cofre In,
/// one operation with a durable transfer identity, both legs or neither, and a cofre with no session or variance.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TransactionControllerSafeDepositTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Deposit_ShouldWriteBothLegsAndTheTransfer_AndReconcileE08()
    {
        // E08: O=1,000; transfer cash 250 → C=750, cofre +250; session I=0 U=250 → 0.
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "SafeE08");
        var sessionId = await s.StartAsync();
        var before = await _client.GetSessionAsync(sessionId, s.MemberA.Token);

        var response = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(250m), s.MemberA.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        response.Headers.ETag.ShouldNotBeNull();
        var created = await response.ReadContentAsync<ResponseCreateTransactionJson>();
        var rows = await s.RowsAsync();
        rows.Count.ShouldBe(2);
        var source = rows.Single(r => r.Id == created.Id);
        var destination = rows.Single(r => r.Id == created.GeneratedTransactionId);
        (source.AccountId, source.Direction, source.Value, source.SessionId, source.GenerationKind)
            .ShouldBe((s.CashboxId, Direction.Out, 250m, (Guid?)sessionId, (TransactionGenerationKind?)null));
        source.TransactionTypeId.ShouldBe(s.SystemTypes[TransactionTypeSystemRole.SafeDepositOut].Id);
        (destination.AccountId, destination.Direction, destination.Value, destination.SessionId)
            .ShouldBe((s.SafeId, Direction.In, 250m, (Guid?)null));
        (destination.GenerationKind, destination.GeneratedFromTransactionId)
            .ShouldBe((TransactionGenerationKind.SafeCashDeposit, (Guid?)source.Id));
        destination.TransactionTypeId.ShouldBe(s.SystemTypes[TransactionTypeSystemRole.SafeDepositIn].Id);

        var transfer = await s.QueryAsync(db => db.CustodyTransfers.AsNoTracking().SingleAsync(t => t.BranchId == s.Branch.Id));
        transfer.Id.ShouldBe(created.CustodyTransferId!.Value);
        (transfer.SourceTransactionId, transfer.DestinationTransactionId, transfer.SourceSessionId, transfer.Value, transfer.AssetKind)
            .ShouldBe((source.Id, destination.Id, (Guid?)sessionId, 250m, CustodyTransferAssetKind.Cash));

        var after = await _client.GetSessionAsync(sessionId, s.MemberA.Token);
        after.Count!.LedgerRevision.ShouldBe(before.Count!.LedgerRevision + 1);
        await s.SaveOkAsync(sessionId, cash: 750m);
        var attempt = (await _client.ConferAsync(sessionId, s.MemberA.Token)).Attempt;
        (attempt.TotalIn, attempt.TotalOut, attempt.Difference).ShouldBe((0m, 250m, 0m));

        var date = (await _client.GetSessionAsync(sessionId, s.MemberA.Token)).Date;
        var resultResponse = await _client.GetAuthAsync($"/report/financial-result?year={date.Year}&month={date.Month}", s.Manager.Token);
        resultResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await resultResponse.Content.ReadAsStringAsync());
        var result = await resultResponse.ReadContentAsync<ResponseFinancialResultJson>();
        result.KnownOwnResult.ShouldBe(0m);
        result.Classes.Single(c => c.FinancialClass == FinancialClass.InternalTransfer).KnownVolume.ShouldBe(250m);
        result.Entries.ShouldContain(e => e.SourceId == source.Id);
        result.Entries.ShouldNotContain(e => e.SourceId == destination.Id);

        // The cofre holds no session and can never start one: its stock never enters a cashbox variance.
        var safeStart = await _client.PostAuthAsync(
            "/session/start", new RequestStartSessionJson { AccountId = s.SafeId }, s.Manager.Token);
        safeStart.StatusCode.ShouldNotBe(HttpStatusCode.Created);
        (await s.QueryAsync(db => db.DailyCloses.CountAsync(d => d.AccountId == s.SafeId))).ShouldBe(0);
    }

    [Fact]
    public async Task Deposit_ShouldReplayTheSameTransfer_AndGuardKeyReuseAndConcurrentDuplicates()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "SafeReplay");
        await s.StartAsync();
        var key = $"deposit-{Guid.NewGuid():N}";

        var first = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(100m), s.MemberA.Token, idempotencyKey: key);
        var replay = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(100m), s.MemberA.Token, idempotencyKey: key);
        var changed = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(101m), s.MemberA.Token, idempotencyKey: key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        replay.StatusCode.ShouldBe(HttpStatusCode.Created);
        var original = await first.ReadContentAsync<ResponseCreateTransactionJson>();
        var replayed = await replay.ReadContentAsync<ResponseCreateTransactionJson>();
        (replayed.Id, replayed.GeneratedTransactionId, replayed.CustodyTransferId)
            .ShouldBe((original.Id, original.GeneratedTransactionId, original.CustodyTransferId));
        changed.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var concurrentKey = $"deposit-{Guid.NewGuid():N}";
        var racing = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(30m), s.MemberA.Token, idempotencyKey: concurrentKey)));
        racing.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created || r.StatusCode == HttpStatusCode.Conflict);
        var ids = new HashSet<Guid>();
        foreach (var r in racing.Where(r => r.StatusCode == HttpStatusCode.Created))
            ids.Add((await r.ReadContentAsync<ResponseCreateTransactionJson>()).CustodyTransferId!.Value);
        ids.Count.ShouldBe(1);

        (await s.QueryAsync(db => db.CustodyTransfers.CountAsync(t => t.BranchId == s.Branch.Id))).ShouldBe(2);
        (await s.RowsAsync()).Count.ShouldBe(4);
    }

    [Fact]
    public async Task InvalidDestinations_ShouldBeRefusedBeforeAnyEffect()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "SafeInvalid");
        await s.StartAsync();
        var bank = await factory.SeedAccountAsync(s.Branch.Id, AccountType.BankAccount, "Banco");
        var inactiveSafe = await factory.SeedAccountAsync(s.Branch.Id, AccountType.Safe, "Cofre antigo", active: false);
        var foreign = await FinancialDayScenario.CreateAsync(factory, _client, "SafeForeign");

        foreach (var destination in new[] { bank.Id, s.TabId, inactiveSafe.Id, foreign.SafeId, Guid.NewGuid() })
        {
            var response = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(10m, destination, byManager: true), s.Manager.Token);
            await ShouldFail(response, HttpStatusCode.NotFound, ResourcesErrorMessages.ACCOUNT_SAFE_NOT_FOUND);
        }
        var same = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(10m, s.CashboxId, byManager: true), s.Manager.Token);
        await ShouldFail(same, HttpStatusCode.BadRequest, ResourcesErrorMessages.SAFE_DEPOSIT_SAME_ACCOUNT);
        var otherSafe = await factory.SeedAccountAsync(s.Branch.Id, AccountType.Safe, "Cofre 2");
        var fromSafe = await _client.PostAuthAsync(
            "/transaction/safe-deposit",
            new RequestSafeDepositJson
            {
                Date = s.Today, Value = 10m, AccountId = s.SafeId, SafeAccountId = otherSafe.Id, RecordedByOperatorId = s.MemberA.Operator!.Id
            },
            s.Manager.Token);
        await ShouldFail(fromSafe, HttpStatusCode.Conflict, ResourcesErrorMessages.SAFE_DEPOSIT_SOURCE_NOT_CASHBOX);

        (await s.RowsAsync()).ShouldBeEmpty();
        (await s.QueryAsync(db => db.CustodyTransfers.CountAsync(t => t.BranchId == s.Branch.Id))).ShouldBe(0);
    }

    [Fact]
    public async Task Grants_ShouldBeCheckedOnBothAccounts_IncludingBeforeAReplay()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "SafeGrants", grantSafeToMemberA: false);
        await s.StartAsync(s.MemberB);

        var withoutGrant = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(10m), s.MemberB.Token);
        await ShouldFail(withoutGrant, HttpStatusCode.Forbidden, ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);

        var grant = await factory.SeedOperatorAccountAsync(s.MemberB.Operator!.Id, s.SafeId);
        var key = $"deposit-{Guid.NewGuid():N}";
        var granted = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(10m), s.MemberB.Token, idempotencyKey: key);
        granted.StatusCode.ShouldBe(HttpStatusCode.Created, await granted.Content.ReadAsStringAsync());

        // Revoking the cofre grant refuses the replay too: a stored outcome is never returned to lost authority.
        await factory.ExecuteSqlAsync($"UPDATE \"OperatorAccounts\" SET \"Active\" = false WHERE \"Id\" = '{grant.Id}'");
        var replay = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(10m), s.MemberB.Token, idempotencyKey: key);
        await ShouldFail(replay, HttpStatusCode.Forbidden, ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);

        // Only the custodian may move the active session's cash.
        await factory.SeedOperatorAccountAsync(s.MemberA.Operator!.Id, s.SafeId);
        var notCustodian = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(10m), s.MemberA.Token);
        await ShouldFail(notCustodian, HttpStatusCode.Forbidden, ResourcesErrorMessages.SESSION_NOT_CUSTODIAN);
        (await s.RowsAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Legs_ShouldFinalizeAndCancelTogether_AndTheGeneratedLegNeverAlone()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "SafeLegs");
        var sessionId = await s.StartAsync();
        var draft = await Created(s.Deposit(80m, saveAsDraft: true), s.MemberA.Token);
        (await s.RowsAsync()).ShouldAllBe(r => r.Status == TransactionStatus.Draft);

        var destination = (await s.RowsAsync()).Single(r => r.Id == draft.GeneratedTransactionId);
        await ShouldFail(
            await _client.PostAuthAsync($"/transaction/{destination.Id}/finalize", s.Manager.Token, expectedVersion: destination.Version),
            HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_GENERATED_ROW_READ_ONLY);

        var finalize = await _client.PostAuthAsync($"/transaction/{draft.Id}/finalize", s.MemberA.Token, expectedVersion: draft.Version);
        finalize.StatusCode.ShouldBe(HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        (await s.RowsAsync()).ShouldAllBe(r => r.Status == TransactionStatus.Active);

        var source = (await s.RowsAsync()).Single(r => r.Id == draft.Id);
        var cancel = await _client.PostAuthAsync(
            $"/transaction/{source.Id}/cancel",
            new RequestCancelTransactionJson { CancellationReason = "Depósito lançado em duplicidade" },
            s.MemberA.Token,
            expectedVersion: source.Version);
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        (await s.RowsAsync()).ShouldAllBe(r => r.Status == TransactionStatus.Cancelled);
        (await s.QueryAsync(db => db.CustodyTransfers.CountAsync(t => t.BranchId == s.Branch.Id))).ShouldBe(1);

        // Ledger effects: the Draft created nothing, finalize and cancel each bumped once.
        var session = await _client.GetSessionAsync(sessionId, s.MemberA.Token);
        session.Count!.LedgerRevision.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LegacyDraftDeposit_ShouldRefuseOneSidedFinalization_WithoutChangingLedger(bool typeActive)
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "SafeLegacyDraft");
        var sessionId = await s.StartAsync();
        var type = s.SystemTypes[TransactionTypeSystemRole.SafeDepositOut];
        var legacy = await factory.SeedTransactionAsync(
            s.Branch.Id, s.CashboxId, type.Id, type.CategoryId, Direction.Out,
            s.MemberA.Operator!.Id, s.MemberA.User.Id,
            date: s.Today, value: 80m, status: TransactionStatus.Draft, sessionId: sessionId);
        if (!typeActive)
            await factory.ExecuteSqlAsync($"UPDATE \"TransactionTypes\" SET \"Active\" = false WHERE \"Id\" = '{type.Id}'");
        var before = await _client.GetSessionAsync(sessionId, s.MemberA.Token);

        var response = await _client.PostAuthAsync(
            $"/transaction/{legacy.Id}/finalize", s.MemberA.Token, expectedVersion: legacy.Version);
        await ShouldFail(response, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_SYSTEM_ONLY);

        var rows = await s.RowsAsync();
        rows.Count.ShouldBe(1);
        rows.Single().Status.ShouldBe(TransactionStatus.Draft);
        (await s.QueryAsync(db => db.CustodyTransfers.CountAsync(t => t.BranchId == s.Branch.Id))).ShouldBe(0);
        var after = await _client.GetSessionAsync(sessionId, s.MemberA.Token);
        after.Count!.LedgerRevision.ShouldBe(before.Count!.LedgerRevision);
    }

    [Theory]
    [InlineData(TransactionTypeSystemRole.SafeDepositOut, true)]
    [InlineData(TransactionTypeSystemRole.SafeDepositOut, false)]
    [InlineData(TransactionTypeSystemRole.BolaoFee, true)]
    [InlineData(TransactionTypeSystemRole.BolaoFee, false)]
    public async Task LegacySystemPlan_ShouldRefuseActivationOfEveryInstallment(TransactionTypeSystemRole role, bool typeActive)
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "LegacyProtectedPlan");
        var sessionId = await s.StartAsync();
        var type = s.SystemTypes[role];
        var originId = Guid.NewGuid();
        foreach (var value in new[] { 20m, 30m })
            await factory.SeedTransactionAsync(
                s.Branch.Id, s.CashboxId, type.Id, type.CategoryId,
                role == TransactionTypeSystemRole.BolaoFee ? Direction.In : Direction.Out,
                s.MemberA.Operator!.Id, s.MemberA.User.Id,
                date: s.Today, value: value, status: TransactionStatus.Draft,
                originTransactionId: originId, sessionId: sessionId, id: value == 20m ? originId : Guid.NewGuid());
        if (!typeActive)
            await factory.ExecuteSqlAsync($"UPDATE \"TransactionTypes\" SET \"Active\" = false WHERE \"Id\" = '{type.Id}'");
        var rows = await s.RowsAsync();
        var before = await _client.GetSessionAsync(sessionId, s.MemberA.Token);
        var response = await _client.PostAuthAsync(
            $"/transaction/installment/{originId}/finalize",
            new RequestFinalizeInstallmentPlanJson
            {
                Installments = rows.Select(row => new RequestInstallmentPlanRowVersionJson { TransactionId = row.Id, Version = row.Version }).ToList()
            }, s.MemberA.Token);
        await ShouldFail(response, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_SYSTEM_ONLY);
        (await s.RowsAsync()).ShouldAllBe(row => row.Status == TransactionStatus.Draft);
        (await _client.GetSessionAsync(sessionId, s.MemberA.Token)).Count!.LedgerRevision.ShouldBe(before.Count!.LedgerRevision);
    }

    [Fact]
    public async Task FailureAfterTheFirstLeg_ShouldLeaveNeitherLegNorTransfer()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "SafeRollback");
        await s.StartAsync();
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();

        HttpResponseMessage response;
        using (interceptor.Arm(
                   command => command.CommandText.Contains("INSERT INTO \"CustodyTransfers\"", StringComparison.Ordinal),
                   _ => throw new InvalidOperationException("Injected transfer failure")))
        {
            response = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(40m), s.MemberA.Token);
        }

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await s.RowsAsync()).ShouldBeEmpty();
        (await s.QueryAsync(db => db.CustodyTransfers.CountAsync(t => t.BranchId == s.Branch.Id))).ShouldBe(0);
    }

    [Fact]
    public async Task OneSidedDepositsAndForgedTransfers_ShouldBeRefused()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "SafeOneSided");
        await s.StartAsync();

        // The deposit type is system-owned: the generic create cannot write a one-sided deposit.
        var generic = await _client.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJson
            {
                Date = s.Today, Value = 10m, AccountId = s.CashboxId,
                TransactionTypeId = s.SystemTypes[TransactionTypeSystemRole.SafeDepositOut].Id
            },
            s.MemberA.Token);
        await ShouldFail(generic, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_SYSTEM_ONLY);

        // The database refuses a transfer whose legs do not match.
        var deposit = await Created(s.Deposit(20m), s.MemberA.Token);
        var forged = await Should.ThrowAsync<PostgresException>(() => factory.ExecuteSqlAsync($"""
            INSERT INTO "CustodyTransfers" ("Id", "BranchId", "AssetKind", "Value", "SourceAccountId", "SourceSessionId",
                "SourceTransactionId", "DestinationAccountId", "DestinationTransactionId", "CreatedByUserId", "Active", "CreatedAt")
            SELECT gen_random_uuid(), "BranchId", 1, 21, "SourceAccountId", "SourceSessionId", "DestinationTransactionId",
                "DestinationAccountId", "SourceTransactionId", "CreatedByUserId", true, now()
            FROM "CustodyTransfers" WHERE "Id" = '{deposit.CustodyTransferId}'
            """));
        forged.ConstraintName.ShouldBe("CK_CustodyTransfers_LegsCoherent");
        var rewritten = await Should.ThrowAsync<PostgresException>(() => factory.ExecuteSqlAsync($"""
            UPDATE "CustodyTransfers" SET "Value" = 1 WHERE "Id" = '{deposit.CustodyTransferId}'
            """));
        rewritten.ConstraintName.ShouldBe("CK_CustodyTransfers_Immutable");
    }

    [Fact]
    public async Task LockedDateAndFrozenSession_ShouldRefuseTheDeposit()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "SafeFrozen");
        var sessionId = await s.StartAsync();
        await s.SaveOkAsync(sessionId, cash: 1_000m);
        await _client.SubmitAsync(sessionId, s.MemberA.Token);

        var frozen = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(10m), s.MemberA.Token);
        await ShouldFail(frozen, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);

        await factory.ExecuteSqlAsync($"""
            UPDATE "Settings" SET "LockDate" = '{s.Today:yyyy-MM-dd}' WHERE "BranchId" = '{s.Branch.Id}'
            """);
        var locked = await _client.PostAuthAsync("/transaction/safe-deposit", s.Deposit(10m, byManager: true), s.Manager.Token);
        await ShouldFail(locked, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
        (await s.RowsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task SafeAccount_ShouldBeCreatedByManagersOnly_AndReadAsSafe()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "SafeAccount");

        var member = await _client.PostAuthAsync("/account/safe", new RequestCreateSafeAccountJson { Name = "Cofre 2" }, s.MemberA.Token);
        member.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var read = await _client.GetAuthAsync($"/account/{s.SafeId}", s.Manager.Token);
        read.StatusCode.ShouldBe(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
        var account = await read.ReadContentAsync<ResponseAccountJson>();
        (account.Type, account.TabAccountId, account.TerminalId).ShouldBe((AccountType.Safe, (Guid?)null, (Guid?)null));
    }

    private async Task<ResponseCreateTransactionJson> Created(RequestSafeDepositJson request, string token)
    {
        var response = await _client.PostAuthAsync("/transaction/safe-deposit", request, token);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseCreateTransactionJson>();
    }

    private static async Task ShouldFail(HttpResponseMessage response, HttpStatusCode status, string key)
    {
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(key);
    }
}
