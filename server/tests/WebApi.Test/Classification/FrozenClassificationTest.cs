using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Cheques;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Classification;

/// <summary>
/// M9.7 Phase 1.3 on real PostgreSQL: the database captures a row's classification when it is posted (insert Active or
/// finalize), a later type edit changes only rows posted afterwards, and the one way a posted snapshot changes is the
/// audited open-period reclassification of the exact rows and versions read.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class FrozenClassificationTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TypeEdits_ShouldOnlyAffectRowsPostedAfterwards_AndADraftShouldTakeItsClassificationWhenFinalized()
    {
        var s = await ChequeScenario.CreateAsync(factory, _client, "Frozen");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 3, 20, 10, 0, 0));
        var type = s.Day.SeededTypes["saidas.telesena"];
        var before = await SeedRowAsync(s, type, new DateTime(2025, 3, 10), 25m);
        var draft = await SeedRowAsync(s, type, new DateTime(2025, 3, 11), 15m, TransactionStatus.Draft);
        draft.ClassificationRevision.ShouldBe(0);
        draft.PostedSettlementInstrument.ShouldBeNull();

        await UpdateTypeAsync(s, type, SettlementInstrument.CreditCard, FinancialClass.CaixaAndClientMoney);
        var after = await SeedRowAsync(s, type, new DateTime(2025, 3, 12), 40m);
        var finalize = await _client.PostAuthAsync($"/transaction/{draft.Id}/finalize", s.ManagerToken, expectedVersion: draft.Version);
        finalize.StatusCode.ShouldBe(HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        var finalized = await finalize.ReadContentAsync<ResponseTransactionJson>();

        // Frozen at posting: the earlier row keeps None; the later one and the Draft finalized after the edit carry it.
        (await ReloadAsync(before.Id)).PostedSettlementInstrument.ShouldBe(SettlementInstrument.None);
        (await ReloadAsync(after.Id)).PostedSettlementInstrument.ShouldBe(SettlementInstrument.CreditCard);
        finalized.Classification!.SettlementInstrument.ShouldBe(SettlementInstrument.CreditCard);
        (finalized.Classification.FinancialClass, finalized.Classification.Provenance, finalized.Classification.Revision)
            .ShouldBe((FinancialClass.CaixaAndClientMoney, ClassificationProvenance.Posting, 1));

        var upcoming = await UpcomingAsync(s, new DateTime(2025, 3, 1), new DateTime(2025, 3, 31));
        upcoming.Items.Select(item => item.TransactionId).Order().ShouldBe(new[] { after.Id, draft.Id }.Order());
        (upcoming.TotalValue, upcoming.IsConfigured).ShouldBe((55m, true));

        // Clearing the type afterwards changes nothing already posted.
        await UpdateTypeAsync(s, type, SettlementInstrument.None, FinancialClass.Unclassified);
        (await UpcomingAsync(s, new DateTime(2025, 3, 1), new DateTime(2025, 3, 31))).TotalValue.ShouldBe(55m);
    }

    [Fact]
    public async Task OpenPeriodReclassification_ShouldRewriteExactlyTheReadRows_WithAnAuditedRevision()
    {
        var s = await ChequeScenario.CreateAsync(factory, _client, "Reclassify");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 2, 20, 10, 0, 0));
        var type = s.Day.SeededTypes["saidas.telesena"];
        var locked = await SeedRowAsync(s, type, new DateTime(2025, 1, 20), 10m);
        var open = await SeedRowAsync(s, type, new DateTime(2025, 2, 5), 20m);
        await s.LockThroughAsync(new DateTime(2025, 1, 31));
        await UpdateTypeAsync(s, type, SettlementInstrument.DebitCard, FinancialClass.CaixaAndClientMoney);

        var read = await CandidatesAsync(s, type, new DateTime(2025, 1, 1), new DateTime(2025, 2, 20));
        read.Rows.Select(row => row.TransactionId).ShouldBe([open.Id]);
        (read.LockDate, read.TargetSettlementInstrument).ShouldBe(((DateTime?)new DateTime(2025, 1, 31), SettlementInstrument.DebitCard));

        var stale = await ReclassifyAsync(s, type, new DateTime(2025, 2, 1), new DateTime(2025, 2, 20), [(open.Id, read.Rows[0].Version + 1)]);
        await ShouldFail(stale, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_STALE);
        var lockedWindow = await ReclassifyAsync(s, type, new DateTime(2025, 1, 31), new DateTime(2025, 2, 20), [(open.Id, read.Rows[0].Version)]);
        await ShouldFail(lockedWindow, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_LOCKED_PERIOD);
        var future = await ReclassifyAsync(s, type, new DateTime(2025, 2, 1), new DateTime(2025, 2, 21), [(open.Id, read.Rows[0].Version)]);
        await ShouldFail(future, HttpStatusCode.BadRequest, ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_WINDOW_IN_FUTURE);
        var noReason = await _client.PostAuthAsync($"/transaction-type/{type.Id}/reclassify",
            new RequestReclassifyTransactionTypeJson
            {
                DateFrom = new DateTime(2025, 2, 1), DateTo = new DateTime(2025, 2, 20), Reason = " ",
                Rows = [new RequestReclassifyRowJson { TransactionId = open.Id, Version = read.Rows[0].Version }]
            }, s.ManagerToken);
        await ShouldFail(noReason, HttpStatusCode.BadRequest, ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_REASON_REQUIRED);

        var ok = await ReclassifyAsync(s, type, new DateTime(2025, 2, 1), new DateTime(2025, 2, 20), [(open.Id, read.Rows[0].Version)]);
        ok.StatusCode.ShouldBe(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        var outcome = await ok.ReadContentAsync<ResponseReclassificationJson>();
        var reclassified = outcome.Rows.Single().Classification!;
        (reclassified.SettlementInstrument, reclassified.Provenance, reclassified.Revision)
            .ShouldBe((SettlementInstrument.DebitCard, ClassificationProvenance.OpenPeriodReclassification, 2));

        var revision = await s.QueryAsync(db => db.TransactionClassificationRevisions.AsNoTracking().SingleAsync(r => r.TransactionId == open.Id));
        (revision.BatchId, revision.Revision, revision.PreviousSettlementInstrument, revision.SettlementInstrument, revision.Reason)
            .ShouldBe((outcome.BatchId!.Value, 2, (SettlementInstrument?)SettlementInstrument.None, SettlementInstrument.DebitCard, "Correção de março"));
        (await ReloadAsync(locked.Id)).PostedSettlementInstrument.ShouldBe(SettlementInstrument.None);
        (await CandidatesAsync(s, type, new DateTime(2025, 2, 1), new DateTime(2025, 2, 20))).Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task Database_ShouldRefuseAnyUnauditedSnapshotChangeAndAnyEventRewrite()
    {
        var s = await ChequeScenario.CreateAsync(factory, _client, "FrozenGuard");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2025, 1, 20, 10, 0, 0));
        var cheque = await s.SeedChequeAsync(new DateTime(2025, 1, 10), 70m);
        await s.DepositOkAsync(new DateTime(2025, 1, 11), (cheque.Id, 0));

        var snapshot = await Should.ThrowAsync<PostgresException>(() => s.QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "Transactions" SET "PostedSettlementInstrument" = 0 WHERE "Id" = {cheque.Id}""")));
        snapshot.ConstraintName.ShouldBe("CK_Transactions_ClassificationFrozen");
        var rewrite = await Should.ThrowAsync<PostgresException>(() => s.QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "SettlementEvents" SET "Amount" = 1 WHERE "ReceivableTransactionId" = {cheque.Id}""")));
        rewrite.ConstraintName.ShouldBe("CK_SettlementEvents_Immutable");
        var erase = await Should.ThrowAsync<PostgresException>(() => s.QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
            $"""DELETE FROM "SettlementEvents" WHERE "ReceivableTransactionId" = {cheque.Id}""")));
        erase.ConstraintName.ShouldBe("CK_SettlementEvents_Immutable");
        var overSettle = await Should.ThrowAsync<PostgresException>(() => s.QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "SettlementEvents" ("Id", "BranchId", "ReceivableTransactionId", "Sequence", "PreviousEventId", "Kind",
                "Amount", "OutstandingDelta", "OccurredOn", "PostedOn", "Provenance", "MigrationIssue", "SettlementInstrument",
                "CustodyToAccountId", "CreatedByUserId", "CreatedAt", "Active")
            SELECT gen_random_uuid(), e."BranchId", e."ReceivableTransactionId", 2, e."Id", 3, 70, -70, e."OccurredOn",
                   e."PostedOn", 1, 0, 1, NULL, e."CreatedByUserId", now(), true
            FROM "SettlementEvents" e WHERE e."ReceivableTransactionId" = {cheque.Id}
            """)));
        overSettle.SqlState.ShouldBe(PostgresErrorCodes.CheckViolation);
        (await s.EventsAsync(cheque.Id)).Count.ShouldBe(1);
    }

    private async Task<Transaction> SeedRowAsync(ChequeScenario s, TransactionType type, DateTime date, decimal value,
        TransactionStatus status = TransactionStatus.Active)
    {
        var row = await factory.SeedTransactionAsync(s.BranchId, s.Day.CashboxId, type.Id, type.CategoryId, Direction.Out,
            s.Day.MemberA.Operator!.Id, s.Day.Manager.User.Id, date: date, value: value, status: status, dueDate: date.AddDays(5));
        return await ReloadAsync(row.Id);
    }

    private async Task<Transaction> ReloadAsync(Guid id) => (await factory.ReloadAsync<Transaction>(id))!;

    private async Task UpdateTypeAsync(ChequeScenario s, TransactionType type, SettlementInstrument instrument, FinancialClass financialClass)
    {
        var response = await _client.PutAuthAsync($"/transaction-type/{type.Id}", new RequestUpdateTransactionTypeJson
        {
            Name = type.Name,
            SettlementRule = type.SettlementRule,
            RequiresTabAccountAndClient = type.RequiresTabAccountAndClient,
            SettlementInstrument = instrument,
            FinancialClass = financialClass
        }, s.ManagerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<ResponseTransactionTypeJson>()).FinancialClass.ShouldBe(financialClass);
    }

    private async Task<ResponseUpcomingSettlementsJson> UpcomingAsync(ChequeScenario s, DateTime from, DateTime to)
    {
        var response = await _client.GetAuthAsync(
            $"/report/upcoming-settlements?DueDateFrom={from:yyyy-MM-dd}&DueDateTo={to:yyyy-MM-dd}&Page=1&PageSize=50", s.ManagerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseUpcomingSettlementsJson>();
    }

    private async Task<ResponseReclassificationJson> CandidatesAsync(ChequeScenario s, TransactionType type, DateTime from, DateTime to)
    {
        var response = await _client.GetAuthAsync(
            $"/transaction-type/{type.Id}/reclassification-candidates?DateFrom={from:yyyy-MM-dd}&DateTo={to:yyyy-MM-dd}", s.ManagerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseReclassificationJson>();
    }

    private Task<HttpResponseMessage> ReclassifyAsync(ChequeScenario s, TransactionType type, DateTime from, DateTime to,
        IEnumerable<(Guid Id, uint Version)> rows) =>
        _client.PostAuthAsync($"/transaction-type/{type.Id}/reclassify", new RequestReclassifyTransactionTypeJson
        {
            DateFrom = from,
            DateTo = to,
            Reason = "Correção de março",
            Rows = rows.Select(row => new RequestReclassifyRowJson { TransactionId = row.Id, Version = row.Version }).ToList()
        }, s.ManagerToken);

    private static async Task ShouldFail(HttpResponseMessage response, HttpStatusCode status, string key)
    {
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(key);
    }
}
