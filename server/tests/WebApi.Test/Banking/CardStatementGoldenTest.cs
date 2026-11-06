using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Cheques;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Banking;

[Collection(ServerApiCollection.Name)]
public class CardStatementGoldenTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();
    private readonly Dictionary<Guid, Guid> accountBindings = [];

    private async Task<ResponseFinancialResultJson> FinancialResult(ChequeScenario scenario)
    {
        var response = await client.GetAuthAsync("/report/financial-result?year=2026&month=10", scenario.ManagerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseFinancialResultJson>();
    }

    [Fact]
    public async Task PartialThenCompleteLockedClaim_MatchesExistingNetReceipts_AndRetainsUnexplainedDifference()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "CardMatch");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var row = await Card(s, 100m);
        var original = (await factory.ReloadAsync<Transaction>(row.Id))!;
        await s.LockThroughAsync(new DateTime(2026, 9, 30));
        var provider = await Provider(s);
        var before = await s.BranchTransactionCountAsync();
        var first = await Settle(s, Batch(provider, row, 0, 40m, 1m, 39m));
        first.Receivables.Single().OutstandingValue.ShouldBe(60m);
        first.BankReceipt.Amount.ShouldBe(39m);
        first.BankReceipt.Allocations.Single().Amount.ShouldBe(40m);
        var second = await Settle(s, Batch(provider, row, 1, 60m, 2m, 57m));
        second.Receivables.Single().OutstandingValue.ShouldBe(0m);
        second.UnmatchedDifference.ShouldBe(-1m);
        var resultBeforeMatch = await FinancialResult(s);
        resultBeforeMatch.KnownOwnResult.ShouldBe(-3m);
        resultBeforeMatch.BankPositionComplete.ShouldBeFalse();
        var imported = await Import(s, Statement(("one", 39m), ("two", 57m)));
        (await s.BranchTransactionCountAsync()).ShouldBe(before);
        var proposal = await Propose(s, imported.Id);
        proposal.Proposals.Count.ShouldBe(1);
        proposal.Proposals[0].ReceiptId.ShouldBe(first.BankReceipt.Id);
        proposal.UnmatchedLineIds.ShouldBe([imported.Lines.Single(l => l.Amount == 57m).Id]);
        var confirmation = ConfirmRequest(proposal.Proposals[0]);
        var key = Guid.NewGuid().ToString();
        var matched = await Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", confirmation, key);
        var replay = await Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", confirmation, key);
        replay.MatchIds.ShouldBe(matched.MatchIds);
        (await FinancialResult(s)).KnownOwnResult.ShouldBe(-3m);
        (await s.ReceiptAsync(first.BankReceipt.Id)).Status.ShouldBe(BankReceiptStatus.Confirmed);
        (await s.ReceiptAsync(second.BankReceipt.Id)).Status.ShouldBe(BankReceiptStatus.Pending);
        var duplicate = await Import(s, Statement(("one", 39m), ("two", 57m)));
        duplicate.Id.ShouldBe(imported.Id);
        duplicate.DuplicateImport.ShouldBeTrue();
        var retained = await client.GetAuthAsync($"/bank-statement/{imported.Id}", s.ManagerToken);
        retained.StatusCode.ShouldBe(HttpStatusCode.OK);
        var read = await retained.ReadContentAsync<ResponseBankStatementJson>();
        read.Lines.Count(l => l.Matched).ShouldBe(1);
        read.Lines.Single(l => !l.Matched).Amount.ShouldBe(57m);
        var providerRead = await client.GetAuthAsync("/card-provider", s.ManagerToken);
        providerRead.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await providerRead.ReadContentAsync<List<ResponseCardProviderJson>>()).Single().Id.ShouldBe(provider.Id);
        (await s.QueryAsync(db => db.BankStatementLines.CountAsync(l => l.BranchId == s.BranchId))).ShouldBe(2);
        (await s.QueryAsync(db => db.BankStatementMatches.CountAsync(l => l.BranchId == s.BranchId))).ShouldBe(1);
        (await s.BranchTransactionCountAsync()).ShouldBe(before);
        var unchanged = (await factory.ReloadAsync<Transaction>(row.Id))!;
        (unchanged.Version, unchanged.Value, unchanged.ClassificationRevision).ShouldBe((original.Version, 100m, 1));
        (await s.ReceivableAsync(row.Id, new DateTime(2026, 9, 30))).OutstandingValue.ShouldBe(100m);
        (await s.QueryAsync(db => db.SettlementEvents.Where(e => e.BranchId == s.BranchId).SumAsync(e => e.Amount))).ShouldBe(100m);
        (await s.QueryAsync(db => db.CardSettlementBatches.Where(b => b.BranchId == s.BranchId).SumAsync(b => b.ConfirmedFee))).ShouldBe(3m);
        (await s.QueryAsync(db => db.BankReceipts.Where(r => r.BranchId == s.BranchId).SumAsync(r => r.Amount))).ShouldBe(97m);
    }

    [Fact]
    public async Task UnknownFeeIsDifferentFromExplicitZero_AndBadItemRollsBackWholeBatch()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "CardUnknown");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var provider = await Provider(s);
        var a = await Card(s, 50m);
        var b = await Card(s, 50m);
        var unknown = await Settle(s, Batch(provider, a, 0, 50m, null, 49m));
        unknown.FeeUnknown.ShouldBeTrue(); unknown.ExpectedNetAmount.ShouldBeNull();
        unknown.BankReceipt.FeeBasis.ShouldBeNull(); unknown.BankReceipt.FeeEvidence.ShouldBeNull();
        foreach (var financialClass in new[] { FinancialClass.ShopFeesAndIncome, FinancialClass.InternalTransfer })
        {
            var filtered = await client.GetAuthAsync($"/report/financial-result?year=2026&month=10&financialClass={financialClass}", s.ManagerToken);
            filtered.StatusCode.ShouldBe(HttpStatusCode.OK, await filtered.Content.ReadAsStringAsync());
            (await filtered.ReadContentAsync<ResponseFinancialResultJson>()).FeeAmountsComplete.ShouldBeFalse();
        }
        var zero = await Settle(s, Batch(provider, b, 0, 50m, 0m, 50m));
        zero.FeeUnknown.ShouldBeFalse(); zero.ExpectedNetAmount.ShouldBe(50m);
        var imported = await Import(s, Statement(("zero", 50m)));
        var proposed = await Propose(s, imported.Id);
        proposed.Proposals.Select(p => p.ReceiptId).ShouldBe([zero.BankReceipt.Id]);
        var c = await Card(s, 100m);
        var bad = Batch(provider, c, 0, 100m, 0m, 100m);
        bad = new RequestCardSettlementJson
        {
            ProviderId = bad.ProviderId, ProviderVersion = bad.ProviderVersion, ExternalIdentity = bad.ExternalIdentity,
            OccurredOn = bad.OccurredOn, PlanningBasisOn = bad.PlanningBasisOn, ConfirmedFee = 0m,
            ManagerConfirmedFee = true, FeeBasis = "zero", FeeEvidence = "synthetic", Items = [bad.Items[0], new() { TransactionId = b.Id, TransactionVersion = b.Version, ExpectedVersion = 0, GrossAmount = 1m }]
        };
        var before = await s.BranchReceiptCountAsync();
        var response = await client.PostAuthAsync("/card-settlement", bad, s.ManagerToken, Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await s.BranchReceiptCountAsync()).ShouldBe(before);
        (await s.EventsAsync(c.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task OverlappingImportsReuseBankIdentity_EqualAmountDifferentIdentitiesRemainDistinct_ChangedIdentityConflicts()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "OfxOverlap");
        await ConfigureBank(s);
        var first = await Import(s, Statement(("a", 10m), ("b", 10m)));
        var overlap = await Import(s, Statement(("b", 10m), ("c", 10m)));
        first.Lines.Single(l => l.BankIdentity == "b").Id.ShouldBe(overlap.Lines.Single(l => l.BankIdentity == "b").Id);
        (await s.QueryAsync(db => db.BankStatementLines.CountAsync(l => l.BranchId == s.BranchId))).ShouldBe(3);
        var conflict = await client.PostAuthAsync("/bank-statement/import", ImportRequest(s, Statement(("a", 11m))), s.ManagerToken, Guid.NewGuid().ToString());
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict, await conflict.Content.ReadAsStringAsync());
        (await s.QueryAsync(db => db.BankStatementImports.CountAsync(l => l.BranchId == s.BranchId))).ShouldBe(2);
    }

    [Fact]
    public async Task StaleDisplayedMatchAndOneInvalidSelectionRefuseAtomically()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "OfxExact");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var provider = await Provider(s);
        var a = await Settle(s, Batch(provider, await Card(s, 20m), 0, 20m, 0m, 20m));
        var b = await Settle(s, Batch(provider, await Card(s, 30m), 0, 30m, 0m, 30m));
        var imported = await Import(s, Statement(("a", 20m), ("b", 30m)));
        var proposals = await Propose(s, imported.Id);
        var good = ConfirmRequest(proposals.Proposals[0]).Items[0];
        var other = proposals.Proposals[1];
        var bad = new RequestBankMatchItemJson { ProposalId = other.Id, LineId = other.LineId, LineVersion = other.LineVersion, ReceiptId = other.ReceiptId, ReceiptVersion = other.ReceiptVersion, Amount = other.Amount + 1, ExpectationFingerprint = other.ExpectationFingerprint };
        var request = new RequestBankMatchConfirmJson { PostedOn = new DateTime(2026, 10, 6), Items = [good, bad] };
        var response = await client.PostAuthAsync("/bank-statement/confirm", request, s.ManagerToken, Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await s.QueryAsync(db => db.BankStatementMatches.CountAsync(m => m.BranchId == s.BranchId))).ShouldBe(0);
        (await s.ReceiptAsync(a.BankReceipt.Id)).Status.ShouldBe(BankReceiptStatus.Pending);
        (await s.ReceiptAsync(b.BankReceipt.Id)).Status.ShouldBe(BankReceiptStatus.Pending);
        var exact = ConfirmRequest(proposals.Proposals[0]);
        var results = await Task.WhenAll(client.PostAuthAsync("/bank-statement/confirm", exact, s.ManagerToken, "a"), client.PostAuthAsync("/bank-statement/confirm", exact, s.ManagerToken, "b"));
        results.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        (await s.QueryAsync(db => db.BankStatementMatches.CountAsync(m => m.BranchId == s.BranchId))).ShouldBe(1);
    }

    [Fact]
    public async Task SafeCollectionPostsOneProtectedPair_StatementOnlyConfirmsExistingCredit()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "SafeMatch");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var request = new RequestBankMovementJson
        {
            Kind = BankMovementKind.SafeCollection, BankAccountId = s.Bank.Id, SafeAccountId = s.Day.SafeId,
            BankTransactionTypeId = s.Day.SeededTypes["entradas.recolhimento-banco"].Id,
            SourceTransactionTypeId = s.Day.SeededTypes["saidas.recolhimento-cofre"].Id,
            RecordedByOperatorId = s.Day.MemberA.Operator!.Id, Amount = 200m,
            OccurredOn = new DateTime(2026, 10, 5), Evidence = "Recolhimento sintético"
        };
        var before = await s.BranchTransactionCountAsync();
        var key = Guid.NewGuid().ToString();
        var movement = await Post<ResponseBankMovementJson>(s, "/bank-movement", request, key);
        var replay = await Post<ResponseBankMovementJson>(s, "/bank-movement", request, key);
        replay.Id.ShouldBe(movement.Id);
        (await s.BranchTransactionCountAsync()).ShouldBe(before + 2);
        movement.BankReceipt.Status.ShouldBe(BankReceiptStatus.Pending);
        var posted = await s.QueryAsync(db => db.Transactions.AsNoTracking().Where(t => t.Id == movement.BankTransactionId || t.Id == movement.SourceTransactionId).ToListAsync());
        posted.Count.ShouldBe(2);
        posted.All(t => t.PostedFinancialClass == FinancialClass.InternalTransfer && t.SessionId == null && t.GenerationKind == TransactionGenerationKind.BankMovement).ShouldBeTrue();
        posted.Sum(t => t.Direction == Direction.In ? t.Value : -t.Value).ShouldBe(0m);
        var import = await Import(s, Statement(("safe", 200m)));
        var proposal = await Propose(s, import.Id);
        await Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", ConfirmRequest(proposal.Proposals.Single()));
        (await s.BranchTransactionCountAsync()).ShouldBe(before + 2);
        var verifiedReceipt = await s.ReceiptAsync(movement.BankReceipt.Id);
        verifiedReceipt.Status.ShouldBe(BankReceiptStatus.Confirmed);
        (verifiedReceipt.BankMovementId, verifiedReceipt.BankTransactionId, verifiedReceipt.SourceTransactionId, verifiedReceipt.SourceAccountId)
            .ShouldBe(((Guid?)movement.Id, (Guid?)movement.BankTransactionId, movement.SourceTransactionId, (Guid?)s.Day.SafeId));
        var cancel = await client.PostAuthAsync($"/transaction/{movement.SourceTransactionId}/cancel", new RequestCancelTransactionJson { CancellationReason = "Não pode cancelar isoladamente" }, s.ManagerToken, expectedVersion: posted.Single(t => t.Id == movement.SourceTransactionId).Version);
        cancel.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(BankMovementKind.CaixaPrizeReimbursement, FinancialClass.CaixaAndClientMoney, Direction.In)]
    [InlineData(BankMovementKind.BankFee, FinancialClass.OperatingExpenseAndLoss, Direction.Out)]
    public async Task ObservedTypedBankMovementHasFrozenClassAndNoFabricatedDrawerSource(BankMovementKind kind, FinancialClass classification, Direction direction)
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "TypedBank" + kind);
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var category = await factory.SeedCategoryAsync(s.BranchId, defaultDirection: direction);
        var type = await factory.SeedTransactionTypeAsync(category.Id);
        await s.QueryAsync(db => db.TransactionTypes.Where(t => t.Id == type.Id).ExecuteUpdateAsync(x => x.SetProperty(t => t.FinancialClass, classification)));
        var request = new RequestBankMovementJson
        {
            Kind = kind, BankAccountId = s.Bank.Id, BankTransactionTypeId = type.Id,
            RecordedByOperatorId = s.Day.MemberA.Operator!.Id, Amount = 15m, ObservedBankMovement = true,
            OccurredOn = new DateTime(2026, 10, 5), Evidence = "Evidência sintética explícita"
        };
        var before = await s.BranchTransactionCountAsync();
        var movement = await Post<ResponseBankMovementJson>(s, "/bank-movement", request);
        movement.SourceTransactionId.ShouldBeNull();
        (await s.BranchTransactionCountAsync()).ShouldBe(before + 1);
        var row = (await factory.ReloadAsync<Transaction>(movement.BankTransactionId))!;
        (row.Direction, row.PostedFinancialClass, row.SessionId).ShouldBe((direction, (FinancialClass?)classification, (Guid?)null));
        await s.QueryAsync(db => db.TransactionTypes.Where(t => t.Id == type.Id).ExecuteUpdateAsync(x => x.SetProperty(t => t.FinancialClass, FinancialClass.ShopFeesAndIncome)));
        (await factory.ReloadAsync<Transaction>(row.Id))!.PostedFinancialClass.ShouldBe(classification);
        var xml = Statement(("typed", 15m));
        if (direction == Direction.Out) xml = xml.Replace("<TRNTYPE>CREDIT", "<TRNTYPE>FEE").Replace("<TRNAMT>15", "<TRNAMT>-15");
        var import = await Import(s, xml);
        var proposal = await Propose(s, import.Id);
        await Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", ConfirmRequest(proposal.Proposals.Single()));
        (await s.BranchTransactionCountAsync()).ShouldBe(before + 1);
    }

    [Fact]
    public async Task OverlappingClaimSettlementSerializes_AndReplayRequiresCurrentAuthority()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "CardRace");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var provider = await Provider(s);
        var row = await Card(s, 100m);
        var request = Batch(provider, row, 0, 100m, 0m, 100m);
        var key = Guid.NewGuid().ToString();
        var otherKey = Guid.NewGuid().ToString();
        var otherRequest = Batch(provider, row, 0, 100m, 0m, 100m);
        var results = await Task.WhenAll(client.PostAuthAsync("/card-settlement", request, s.ManagerToken, key), client.PostAuthAsync("/card-settlement", otherRequest, s.ManagerToken, otherKey));
        var firstWon = results[0].StatusCode == HttpStatusCode.Created;
        key = firstWon ? key : otherKey;
        request = firstWon ? request : otherRequest;
        results.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        (await s.EventsAsync(row.Id)).Count.ShouldBe(1);
        (await s.BranchReceiptCountAsync()).ShouldBe(1);
        await s.QueryAsync(db => db.BranchUsers.Where(b => b.BranchId == s.BranchId && b.UserId == s.Day.Manager.User.Id).ExecuteUpdateAsync(x => x.SetProperty(b => b.Role, Role.Member)));
        var forbidden = await client.PostAuthAsync("/card-settlement", request, s.ManagerToken, key);
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await s.BranchReceiptCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task ReturnedChequeInvalidatesDisplayedProposal_WithoutChangingImportedEvidence()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "ReturnedProposal");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var row = await s.SeedChequeAsync(new DateTime(2026, 9, 25), 100m, s.Day.ClientId);
        var deposit = await s.DepositOkAsync(new DateTime(2026, 10, 5), (row.Id, 0));
        var import = await Import(s, Statement(("cheque", 100m)));
        var proposal = (await Propose(s, import.Id)).Proposals.Single();
        var returned = await s.ReturnAsync(row.Id, deposit.Receivables.Single().Events.Single().Id, 1, new DateTime(2026, 10, 6), s.Day.ClientId);
        returned.StatusCode.ShouldBe(HttpStatusCode.OK, await returned.Content.ReadAsStringAsync());
        var response = await client.PostAuthAsync("/bank-statement/confirm", ConfirmRequest(proposal), s.ManagerToken, Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await s.QueryAsync(db => db.BankStatementMatches.CountAsync(m => m.BranchId == s.BranchId))).ShouldBe(0);
        (await s.QueryAsync(db => db.BankStatementLines.SingleAsync(l => l.Id == proposal.LineId))).Amount.ShouldBe(100m);
        (await s.ReceiptAsync(deposit.BankReceipt!.Id)).ExpectedAmount.ShouldBe(0m);
    }

    [Theory]
    [InlineData("card")]
    [InlineData("import")]
    [InlineData("confirm")]
    [InlineData("movement")]
    [InlineData("fee")]
    public async Task FailureAtReplayPersistenceRollsBackEveryEffect_AndSameKeyCanRetry(string operation)
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "BankRollback" + operation);
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var provider = await Provider(s);
        var row = await Card(s, 100m);
        var batch = Batch(provider, row, 0, 100m, 0m, 100m);
        string route; object request;
        if (operation == "card") { route = "/card-settlement"; request = batch; }
        else if (operation == "import") { route = "/bank-statement/import"; request = ImportRequest(s, Statement(("rollback", 100m))); }
        else if (operation == "fee")
        {
            var settled = await Settle(s, batch);
            route = "/card-settlement/fee-evidence"; request = FeeEvidence(settled, 0, 0m);
        }
        else if (operation == "confirm")
        {
            await Settle(s, batch);
            var import = await Import(s, Statement(("rollback", 100m)));
            route = "/bank-statement/confirm"; request = ConfirmRequest((await Propose(s, import.Id)).Proposals.Single());
        }
        else
        {
            route = "/bank-movement";
            request = new RequestBankMovementJson
            {
                Kind = BankMovementKind.SafeCollection, BankAccountId = s.Bank.Id, SafeAccountId = s.Day.SafeId,
                BankTransactionTypeId = s.Day.SeededTypes["entradas.recolhimento-banco"].Id, SourceTransactionTypeId = s.Day.SeededTypes["saidas.recolhimento-cofre"].Id,
                RecordedByOperatorId = s.Day.MemberA.Operator!.Id, Amount = 100m, OccurredOn = new DateTime(2026, 10, 5), Evidence = "Synthetic rollback proof"
            };
        }
        async Task<string> Counts() => await s.QueryAsync(async db => string.Join(",",
            await db.Transactions.CountAsync(t => t.BranchId == s.BranchId), await db.SettlementEvents.CountAsync(e => e.BranchId == s.BranchId),
            await db.BankReceipts.CountAsync(r => r.BranchId == s.BranchId), await db.CardSettlementBatches.CountAsync(b => b.BranchId == s.BranchId),
            await db.BankStatementImports.CountAsync(i => i.BranchId == s.BranchId), await db.BankStatementLines.CountAsync(l => l.BranchId == s.BranchId),
            await db.BankStatementMatches.CountAsync(m => m.BranchId == s.BranchId), await db.BankMovements.CountAsync(m => m.BranchId == s.BranchId),
            await db.CardSettlementFeeEvidence.CountAsync(e => e.BranchId == s.BranchId)));
        var before = await Counts();
        var key = Guid.NewGuid().ToString();
        using (factory.Services.GetRequiredService<ArmableCommandInterceptor>().Arm(
            c => c.CommandText.Contains(operation == "confirm" ? "INSERT INTO \"IdempotencyRequests\"" : "UPDATE \"IdempotencyRequests\"", StringComparison.Ordinal),
            _ => throw new InvalidOperationException("Synthetic banking rollback")))
        {
            var failed = await client.PostAuthAsync(route, request, s.ManagerToken, key);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        (await Counts()).ShouldBe(before);
        var retry = await client.PostAuthAsync(route, request, s.ManagerToken, key);
        retry.IsSuccessStatusCode.ShouldBeTrue(await retry.Content.ReadAsStringAsync());
        var after = await Counts();
        after.ShouldNotBe(before);
        var replay = await client.PostAuthAsync(route, request, s.ManagerToken, key);
        replay.IsSuccessStatusCode.ShouldBeTrue(await replay.Content.ReadAsStringAsync());
        (await Counts()).ShouldBe(after);
    }

    [Fact]
    public async Task ReturnAfterConfirmedChequeDepositExpectsOneTraceableDebit_WithoutAnotherClaimPayment()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "ConfirmedReturn");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var row = await s.SeedChequeAsync(new DateTime(2026, 9, 25), 100m, s.Day.ClientId);
        var deposit = await s.DepositOkAsync(new DateTime(2026, 10, 5), (row.Id, 0));
        var import = await Import(s, Statement(("credit", 100m)));
        await Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", ConfirmRequest((await Propose(s, import.Id)).Proposals.Single()));
        var before = await s.BranchTransactionCountAsync();
        var response = await s.ReturnAsync(row.Id, deposit.Receivables.Single().Events.Single().Id, 1, new DateTime(2026, 10, 6), s.Day.ClientId);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var returned = await response.ReadContentAsync<ResponseChequeOperationJson>();
        returned.Receivables.Single().OutstandingValue.ShouldBe(100m);
        returned.ReturnBankReceipt.ShouldNotBeNull();
        returned.ReturnBankReceipt.IsDebit.ShouldBeTrue();
        returned.ReturnBankReceipt.ReturnReceivableTransactionId.ShouldBe(row.Id);
        returned.ReturnBankReceipt.OriginalBankReceiptId.ShouldBe(deposit.BankReceipt!.Id);
        returned.ReturnBankReceipt.SourceSettlementEventId.ShouldBe(returned.Receivables.Single().Events.Last().Id);
        var debit = Statement(("return", 100m)).Replace("<TRNTYPE>CREDIT", "<TRNTYPE>DEBIT").Replace("<TRNAMT>100", "<TRNAMT>-100");
        var debitImport = await Import(s, debit);
        var proposal = (await Propose(s, debitImport.Id)).Proposals.Single();
        proposal.ReceiptId.ShouldBe(returned.ReturnBankReceipt.Id); proposal.Amount.ShouldBe(-100m);
        await Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", ConfirmRequest(proposal));
        (await s.BranchTransactionCountAsync()).ShouldBe(before);
        (await s.QueryAsync(db => db.BankStatementMatches.CountAsync(m => m.BranchId == s.BranchId))).ShouldBe(2);
        (await s.EventsAsync(row.Id)).Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(SettlementInstrument.Pix, "saidas.pix")]
    [InlineData(SettlementInstrument.MarketPlace, "saidas.marketplace")]
    public async Task ElectronicInstrumentCreatesOneExpectedCreditWithoutDuplicatingDrawerLeg(SettlementInstrument instrument, string seed)
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "Electronic" + instrument);
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var provider = await Post<ResponseCardProviderJson>(s, "/card-provider", new RequestCardProviderJson { Name = "Sintético", ExternalIdentity = "synthetic", Brand = "Teste", Instrument = instrument, BankAccountId = s.Bank.Id, DelayDays = 0, BusinessDays = false });
        var type = s.Day.SeededTypes[seed];
        var seeded = await factory.SeedTransactionAsync(s.BranchId, s.Day.CashboxId, type.Id, type.CategoryId, Direction.Out, s.Day.MemberA.Operator!.Id, s.Day.Manager.User.Id, new DateTime(2026, 9, 25), 100m);
        var row = (await factory.ReloadAsync<Transaction>(seeded.Id))!;
        var before = await s.BranchTransactionCountAsync();
        var batch = await Settle(s, Batch(provider, row, 0, 100m, 0m, 100m));
        batch.Receivables.Single().SettlementInstrument.ShouldBe(instrument);
        var import = await Import(s, Statement(("electronic", 100m)));
        await Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", ConfirmRequest((await Propose(s, import.Id)).Proposals.Single()));
        (await s.BranchTransactionCountAsync()).ShouldBe(before);
        (await s.EventsAsync(row.Id)).Single().OutstandingDelta.ShouldBe(-100m);
    }

    [Theory]
    [InlineData("same")]
    [InlineData("inactive")]
    [InlineData("wrong-kind")]
    [InlineData("foreign")]
    [InlineData("locked")]
    public async Task CollectionRejectsInvalidSourceWithoutAnyPostedLegOrReceipt(string invalid)
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "CollectionInvalid" + invalid);
        var foreign = invalid == "foreign" ? await ChequeScenario.CreateAsync(factory, client, "ForeignCollection") : null;
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var safe = invalid == "same" ? s.Bank.Id : invalid == "wrong-kind" ? s.Day.TabId : foreign?.Day.SafeId ?? s.Day.SafeId;
        if (invalid == "locked") await s.LockThroughAsync(new DateTime(2026, 10, 5));
        if (invalid == "inactive") await s.QueryAsync(db => db.Accounts.Where(a => a.Id == safe).ExecuteUpdateAsync(x => x.SetProperty(a => a.Active, false)));
        var before = await s.BranchTransactionCountAsync();
        var response = await client.PostAuthAsync("/bank-movement", new RequestBankMovementJson
        {
            Kind = BankMovementKind.SafeCollection, BankAccountId = s.Bank.Id, SafeAccountId = safe,
            BankTransactionTypeId = s.Day.SeededTypes["entradas.recolhimento-banco"].Id, SourceTransactionTypeId = s.Day.SeededTypes["saidas.recolhimento-cofre"].Id,
            RecordedByOperatorId = s.Day.MemberA.Operator!.Id, Amount = 100m, OccurredOn = new DateTime(2026, 10, 5),
            PostedOn = invalid == "locked" ? new DateTime(2026, 10, 5) : null, Evidence = "Synthetic invalid source"
        }, s.ManagerToken, Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(invalid == "same" ? HttpStatusCode.BadRequest : invalid == "locked" ? HttpStatusCode.Conflict : HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
        (await s.BranchTransactionCountAsync()).ShouldBe(before);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task ReimbursementCannotBorrowAnUnrelatedSystemTypeOrRecordAnUnknownArrival()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "ReimbursementPolicy");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var before = await s.BranchTransactionCountAsync();
        foreach (var observed in new[] { false, true })
        {
            var response = await client.PostAuthAsync("/bank-movement", new RequestBankMovementJson
            {
                Kind = BankMovementKind.CaixaPrizeReimbursement, BankAccountId = s.Bank.Id,
                BankTransactionTypeId = s.Day.SystemTypes[TransactionTypeSystemRole.FiadoCashRepaymentCounterpart].Id,
                RecordedByOperatorId = s.Day.MemberA.Operator!.Id, Amount = 100m, OccurredOn = new DateTime(2026, 10, 5),
                Evidence = "Synthetic unrelated system classification", ObservedBankMovement = observed
            }, s.ManagerToken, Guid.NewGuid().ToString());
            response.StatusCode.ShouldBe(observed ? HttpStatusCode.Conflict : HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        }
        (await s.BranchTransactionCountAsync()).ShouldBe(before);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AmbiguousMissingBankIdentitiesRemainSeparateUnmatchedEvidence_AndExactRetryAddsNothing()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "AmbiguousOfx");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var provider = await Provider(s);
        await Settle(s, Batch(provider, await Card(s, 10m), 0, 10m, 0m, 10m));
        var xml = Statement(("a", 10m), ("b", 10m)).Replace("<FITID>a</FITID>", "").Replace("<FITID>b</FITID>", "");
        var import = await Import(s, xml);
        import.Lines.Count.ShouldBe(2);
        import.Lines.Select(l => l.Id).Distinct().Count().ShouldBe(2);
        import.Lines.ShouldAllBe(l => l.AmbiguousIdentity && !l.Matched);
        var proposed = await Propose(s, import.Id);
        proposed.Proposals.ShouldBeEmpty();
        proposed.UnmatchedLineIds.Count.ShouldBe(2);
        (await Import(s, xml)).Id.ShouldBe(import.Id);
        (await s.QueryAsync(db => db.BankStatementLines.CountAsync(l => l.BranchId == s.BranchId))).ShouldBe(2);
        (await s.QueryAsync(db => db.BankStatementMatches.CountAsync(m => m.BranchId == s.BranchId))).ShouldBe(0);
    }

    [Fact]
    public async Task InvalidAccountCurrencyTruncationAndOversizeImportHaveNoPersistedEvidenceOrFinancialEffect()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "BadOfx");
        await ConfigureBank(s);
        var before = await s.BranchTransactionCountAsync();
        var valid = Statement(("a", 10m));
        var invalid = new[] { valid.Replace("BRL", "USD"), valid.Replace("fake-account", "different-account"), valid.Replace("</OFX>", ""), new string('x', server.Application.Services.Banking.OfxStatementParser.MaxBytes + 1) };
        foreach (var source in invalid)
        {
            var response = await client.PostAuthAsync("/bank-statement/import", ImportRequest(s, source), s.ManagerToken, Guid.NewGuid().ToString());
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        }
        (await s.QueryAsync(db => db.BankStatementImports.CountAsync(i => i.BranchId == s.BranchId))).ShouldBe(0);
        (await s.QueryAsync(db => db.BankStatementLines.CountAsync(l => l.BranchId == s.BranchId))).ShouldBe(0);
        (await s.BranchTransactionCountAsync()).ShouldBe(before);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task AuthorityChangedAfterInitialAuthorizationIsRecheckedUnderCoordination_AndKeyRemainsRetryable()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "BankAuthorityRace");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var provider = await Provider(s);
        var request = Batch(provider, await Card(s, 100m), 0, 100m, 0m, 100m);
        var key = Guid.NewGuid().ToString();
        var hookRan = false;
        using (factory.Services.GetRequiredService<ArmableCommandInterceptor>().Arm(
            c => c.CommandText.Contains("FROM \"CardProviders\"", StringComparison.Ordinal),
            async _ =>
            {
                hookRan = true;
                await s.QueryAsync(db => db.BranchUsers.Where(b => b.BranchId == s.BranchId && b.UserId == s.Day.Manager.User.Id).ExecuteUpdateAsync(x => x.SetProperty(b => b.Role, Role.Member)));
            }))
        {
            var response = await client.PostAuthAsync("/card-settlement", request, s.ManagerToken, key);
            hookRan.ShouldBeTrue();
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        }
        (await s.BranchEventCountAsync()).ShouldBe(0);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);
        await s.QueryAsync(db => db.BranchUsers.Where(b => b.BranchId == s.BranchId && b.UserId == s.Day.Manager.User.Id).ExecuteUpdateAsync(x => x.SetProperty(b => b.Role, Role.Manager)));
        await Post<ResponseCardSettlementJson>(s, "/card-settlement", request, key);
        (await s.BranchEventCountAsync()).ShouldBe(1);
        (await s.BranchReceiptCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentSameKeySettlementReturnsOneOriginalBatchAndReceipt()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "BankSameKey");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var provider = await Provider(s);
        var request = Batch(provider, await Card(s, 100m), 0, 100m, 0m, 100m);
        var key = Guid.NewGuid().ToString();
        var results = await Task.WhenAll(Post<ResponseCardSettlementJson>(s, "/card-settlement", request, key), Post<ResponseCardSettlementJson>(s, "/card-settlement", request, key));
        results[0].Id.ShouldBe(results[1].Id);
        results[0].BankReceipt.Id.ShouldBe(results[1].BankReceipt.Id);
        (await s.BranchEventCountAsync()).ShouldBe(1);
        (await s.BranchReceiptCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task InstallmentClaimsKeepPostedElectronicIdentityAfterTypeRenameAndClearing_AndCannotBeDepositedAsCheques()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "CardInstallments");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var type = s.Day.SeededTypes["saidas.cartao-credito"];
        var origin = Guid.NewGuid();
        var first = await factory.SeedTransactionAsync(s.BranchId, s.Day.CashboxId, type.Id, type.CategoryId, Direction.Out, s.Day.MemberA.Operator!.Id, s.Day.Manager.User.Id,
            date: new DateTime(2026, 9, 25), value: 40m, originTransactionId: origin, id: origin);
        var second = await factory.SeedTransactionAsync(s.BranchId, s.Day.CashboxId, type.Id, type.CategoryId, Direction.Out, s.Day.MemberA.Operator!.Id, s.Day.Manager.User.Id,
            date: new DateTime(2026, 9, 25), value: 60m, originTransactionId: origin);
        first = (await factory.ReloadAsync<Transaction>(first.Id))!;
        second = (await factory.ReloadAsync<Transaction>(second.Id))!;
        await s.QueryAsync(db => db.TransactionTypes.Where(t => t.Id == type.Id).ExecuteUpdateAsync(x => x.SetProperty(t => t.Name, "Configuração alterada").SetProperty(t => t.SettlementInstrument, SettlementInstrument.None)));
        await s.LockThroughAsync(new DateTime(2026, 9, 30));
        (await s.OpenChequesAsync(new DateTime(2026, 9, 30))).OutstandingTotal.ShouldBe(0m);
        var wrong = await s.DepositAsync(new DateTime(2026, 10, 5), [(first.Id, 0)]);
        wrong.StatusCode.ShouldBe(HttpStatusCode.Conflict, await wrong.Content.ReadAsStringAsync());
        var provider = await Provider(s);
        var request = new RequestCardSettlementJson
        {
            ProviderId = provider.Id, ProviderVersion = provider.Version, ExternalIdentity = "synthetic-installments",
            PlanningBasisOn = new DateTime(2026, 9, 25), OccurredOn = new DateTime(2026, 10, 5),
            ConfirmedFee = 0m, ManagerConfirmedFee = true, FeeBasis = "Zero explicitly confirmed", FeeEvidence = "Synthetic evidence",
            Items = [new() { TransactionId = first.Id, TransactionVersion = first.Version, GrossAmount = 40m }, new() { TransactionId = second.Id, TransactionVersion = second.Version, GrossAmount = 60m }]
        };
        var result = await Settle(s, request);
        result.BankReceipt.Amount.ShouldBe(100m);
        result.BankReceipt.Allocations.Count.ShouldBe(2);
        result.Receivables.ShouldAllBe(r => !r.IsCheque && r.OutstandingValue == 0 && r.SettlementInstrument == SettlementInstrument.CreditCard);
        (await factory.ReloadAsync<Transaction>(first.Id))!.Version.ShouldBe(first.Version);
        (await factory.ReloadAsync<Transaction>(second.Id))!.Version.ShouldBe(second.Version);
        (await s.BranchEventCountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task UnexplainedObservedDifferenceBlocksConfirmationEvenWhenAnotherLineEqualsCalculatedNet()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "SourceDifference");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var provider = await Provider(s);
        var result = await Settle(s, Batch(provider, await Card(s, 100m), 0, 100m, 2m, 97m));
        result.ExpectedNetAmount.ShouldBe(98m);
        result.UnmatchedDifference.ShouldBe(-1m);
        var import = await Import(s, Statement(("short", 97m), ("different-evidence", 98m)));
        var proposal = await Propose(s, import.Id);
        proposal.Proposals.ShouldBeEmpty();
        proposal.UnmatchedLineIds.Count.ShouldBe(2);
        (await s.ReceiptAsync(result.BankReceipt.Id)).Status.ShouldBe(BankReceiptStatus.Pending);
        (await s.QueryAsync(db => db.BankStatementMatches.CountAsync(m => m.BranchId == s.BranchId))).ShouldBe(0);
    }

    [Fact]
    public async Task InvalidClaimTypeTenantStateVersionAmountOrDuplicateAbortsTheEntireBatch()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "CardEligibility");
        var foreignScenario = await ChequeScenario.CreateAsync(factory, client, "ForeignCardEligibility");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var provider = await Provider(s);
        var valid = await Card(s, 100m);
        var other = await Card(s, 100m);
        var cheque = await s.SeedChequeAsync(new DateTime(2026, 9, 25), 100m);
        var type = s.Day.SeededTypes["saidas.cartao-credito"];
        var draft = await factory.SeedTransactionAsync(s.BranchId, s.Day.CashboxId, type.Id, type.CategoryId, Direction.Out, s.Day.MemberA.Operator!.Id, s.Day.Manager.User.Id, date: new DateTime(2026, 9, 25), value: 100m, status: TransactionStatus.Draft);
        var cancelled = await factory.SeedTransactionAsync(s.BranchId, s.Day.CashboxId, type.Id, type.CategoryId, Direction.Out, s.Day.MemberA.Operator!.Id, s.Day.Manager.User.Id, date: new DateTime(2026, 9, 25), value: 100m, status: TransactionStatus.Cancelled);
        var foreign = await Card(foreignScenario, 100m);
        RequestCardSettlementItemJson Item(Transaction row, decimal amount = 100m, bool stale = false) => new() { TransactionId = row.Id, TransactionVersion = row.Version + (stale ? 1u : 0u), GrossAmount = amount };
        var cases = new (RequestCardSettlementItemJson Item, HttpStatusCode Status)[]
        {
            (Item((await factory.ReloadAsync<Transaction>(cheque.Id))!), HttpStatusCode.Conflict),
            (Item((await factory.ReloadAsync<Transaction>(draft.Id))!), HttpStatusCode.Conflict),
            (Item((await factory.ReloadAsync<Transaction>(cancelled.Id))!), HttpStatusCode.Conflict),
            (Item(foreign), HttpStatusCode.NotFound),
            (Item(other, stale: true), HttpStatusCode.Conflict),
            (Item(other, 101m), HttpStatusCode.Conflict),
            (Item(other, 0.001m), HttpStatusCode.BadRequest),
            (Item(valid), HttpStatusCode.BadRequest)
        };
        foreach (var test in cases)
        {
            var request = new RequestCardSettlementJson
            {
                ProviderId = provider.Id, ProviderVersion = provider.Version, ExternalIdentity = Guid.NewGuid().ToString(),
                PlanningBasisOn = new DateTime(2026, 9, 25), OccurredOn = new DateTime(2026, 10, 5),
                ConfirmedFee = 0, ManagerConfirmedFee = true, FeeBasis = "Explicit synthetic zero", FeeEvidence = "Synthetic evidence",
                Items = [Item(valid), test.Item]
            };
            var response = await client.PostAuthAsync("/card-settlement", request, s.ManagerToken, Guid.NewGuid().ToString());
            response.StatusCode.ShouldBe(test.Status, await response.Content.ReadAsStringAsync());
            (await s.BranchReceiptCountAsync()).ShouldBe(0);
            (await s.BranchEventCountAsync()).ShouldBe(0);
            (await s.QueryAsync(db => db.CardSettlementBatches.CountAsync(b => b.BranchId == s.BranchId))).ShouldBe(0);
        }
    }

    [Fact]
    public async Task PixFiadoRepaymentExpectsBankArrivalWithoutAnotherDebtPaymentOrDrawerLeg()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "FiadoPixMatch");
        var today = s.Day.Today;
        using var clock = factory.FreezeBranchLocalTime(today.AddHours(10));
        await ConfigureBank(s);
        await s.Day.SeedOpeningDebtAsync(100m);
        var paymentResponse = await client.PostAuthAsync("/transaction/fiado-payment", s.Day.Repayment(s.Day.PixMethod, 50m), s.Day.MemberA.Token);
        paymentResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await paymentResponse.Content.ReadAsStringAsync());
        var payment = await paymentResponse.ReadContentAsync<ResponseCreateTransactionJson>();
        payment.GeneratedTransactionId.ShouldBeNull();
        var row = (await factory.ReloadAsync<Transaction>(payment.Id))!;
        row.PostedSettlementInstrument.ShouldBe(SettlementInstrument.Pix);
        (await s.FiadoBalanceAsync(s.Day.ClientId, today)).ShouldBe(50m);
        var before = await s.BranchTransactionCountAsync();
        var provider = await Post<ResponseCardProviderJson>(s, "/card-provider", new RequestCardProviderJson
        {
            Name = "PIX sintético", ExternalIdentity = "synthetic-pix", Brand = "PIX", Instrument = SettlementInstrument.Pix,
            BankAccountId = s.Bank.Id, DelayDays = 0, BusinessDays = false
        });
        var batch = await Settle(s, new RequestCardSettlementJson
        {
            ProviderId = provider.Id, ProviderVersion = provider.Version, ExternalIdentity = "synthetic-repayment",
            PlanningBasisOn = today, OccurredOn = today, ConfirmedFee = 0, ManagerConfirmedFee = true,
            FeeBasis = "Explicit synthetic zero", FeeEvidence = "Synthetic evidence",
            Items = [new() { TransactionId = row.Id, TransactionVersion = row.Version, GrossAmount = 50m }]
        });
        batch.BankReceipt.ExpectedAmount.ShouldBe(50m);
        var date = today.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        var source = Statement(("fiado-pix", 50m)).Replace("20261001", date).Replace("20261031", date).Replace("20261006", date);
        var import = await Import(s, source);
        var proposal = (await Propose(s, import.Id)).Proposals.Single();
        var selection = ConfirmRequest(proposal);
        await Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", new RequestBankMatchConfirmJson { PostedOn = today, Items = selection.Items });
        (await s.BranchTransactionCountAsync()).ShouldBe(before);
        (await s.FiadoBalanceAsync(s.Day.ClientId, today)).ShouldBe(50m);
        (await factory.ReloadAsync<Transaction>(row.Id))!.Version.ShouldBe(row.Version);
    }

    [Fact]
    public async Task OfxIdentityIsExplicitAndImmutable_PreservesDisplayFields_AndCannotBeUsedForAnotherAccount()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "OfxBinding");
        await ConfigureBank(s);
        var bank = (await factory.ReloadAsync<Account>(s.Bank.Id))!;
        (bank.Institution, bank.Number).ShouldBe(("Banco sintético", "Conta de exibição"));
        var response = await client.GetAuthAsync($"/bank-statement/account-binding?bankAccountId={s.Bank.Id}", s.ManagerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var binding = (await response.ReadContentAsync<List<ResponseBankStatementAccountBindingJson>>()).Single();
        (binding.Id, binding.BankIdentity, binding.AccountIdentity, binding.Currency).ShouldBe((accountBindings[s.Bank.Id], "104", "fake-account", "BRL"));
        binding.Version.ShouldBeGreaterThan(0u);
        var duplicate = await client.PostAuthAsync("/bank-statement/account-binding", new RequestBankStatementAccountBindingJson { BankAccountId = s.Bank.Id, BankIdentity = "033", AccountIdentity = "different" }, s.ManagerToken, Guid.NewGuid().ToString());
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var anotherBank = await factory.SeedAccountAsync(s.BranchId, AccountType.BankAccount, "Outra conta sintética");
        var duplicateSource = await client.PostAuthAsync("/bank-statement/account-binding", new RequestBankStatementAccountBindingJson
        {
            BankAccountId = anotherBank.Id, BankIdentity = "104", AccountIdentity = "fake-account"
        }, s.ManagerToken, Guid.NewGuid().ToString());
        duplicateSource.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var import = ImportRequest(s, Statement(("foreign-account", 10m)));
        var wrong = await client.PostAuthAsync("/bank-statement/import", new RequestBankStatementImportJson
        {
            AccountBindingId = import.AccountBindingId, BankAccountId = anotherBank.Id,
            BankIdentity = import.BankIdentity, AccountIdentity = import.AccountIdentity, ContentBase64 = import.ContentBase64
        }, s.ManagerToken, Guid.NewGuid().ToString());
        wrong.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await wrong.Content.ReadAsStringAsync());
        (await s.QueryAsync(db => db.BankStatementImports.CountAsync(i => i.BranchId == s.BranchId))).ShouldBe(0);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);
        (await s.QueryAsync(db => db.BankStatementAccountBindings.CountAsync(b => b.BranchId == s.BranchId))).ShouldBe(1);
    }

    [Fact]
    public async Task DelayedBankArrivalDoesNotAssumeAnUnverifiedMaximumTransitWindow()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "DelayedBankArrival");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var row = await s.SeedChequeAsync(new DateTime(2026, 8, 25), 100m);
        await s.LockThroughAsync(new DateTime(2026, 8, 31));
        var deposit = await s.DepositOkAsync(new DateTime(2026, 9, 1), (row.Id, 0));
        var import = await Import(s, Statement(("delayed-arrival", 100m)));
        var proposal = (await Propose(s, import.Id)).Proposals.Single();
        proposal.ReceiptId.ShouldBe(deposit.BankReceipt!.Id);
        await Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", ConfirmRequest(proposal));
        (await s.ReceiptAsync(deposit.BankReceipt.Id)).Status.ShouldBe(BankReceiptStatus.Confirmed);
        (await s.ReceivableAsync(row.Id, new DateTime(2026, 8, 31))).OutstandingValue.ShouldBe(100m);
    }

    [Fact]
    public async Task OnePhysicalOfxSourceCannotBeBoundConcurrentlyToTwoLedgerAccounts()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "BindingRace");
        var other = await factory.SeedAccountAsync(s.BranchId, AccountType.BankAccount, "Outra conta de origem");
        var before = await s.BranchTransactionCountAsync();
        var results = await Task.WhenAll(new[] { s.Bank.Id, other.Id }.Select(account => client.PostAuthAsync(
            "/bank-statement/account-binding", new RequestBankStatementAccountBindingJson { BankAccountId = account, BankIdentity = "104", AccountIdentity = "same-physical-source" },
            s.ManagerToken, Guid.NewGuid().ToString())));
        results.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        (await s.QueryAsync(db => db.BankStatementAccountBindings.CountAsync(b => b.BranchId == s.BranchId))).ShouldBe(1);
        (await s.BranchTransactionCountAsync()).ShouldBe(before);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData("card")]
    [InlineData("cheque")]
    public async Task CachedControllerAuthorityCannotReplayAfterConcurrentRevocation(string operation)
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "ReplayAuthority" + operation);
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var key = Guid.NewGuid().ToString();
        string route; object request;
        if (operation == "card")
        {
            var provider = await Provider(s);
            request = Batch(provider, await Card(s, 100m), 0, 100m, 0m, 100m);
            route = "/card-settlement";
        }
        else
        {
            var row = await s.SeedChequeAsync(new DateTime(2026, 9, 25), 100m);
            request = new RequestChequeDepositJson { BankAccountId = s.Bank.Id, OccurredOn = new DateTime(2026, 10, 5), Items = [new() { TransactionId = row.Id }] };
            route = "/cheque/deposit";
        }
        var success = await client.PostAuthAsync(route, request, s.ManagerToken, key);
        success.StatusCode.ShouldBe(HttpStatusCode.Created, await success.Content.ReadAsStringAsync());
        var hooked = false;
        using (factory.Services.GetRequiredService<ArmableCommandInterceptor>().Arm(
            c => c.CommandText.Contains("FROM \"BranchUsers\"", StringComparison.Ordinal),
            async _ =>
            {
                hooked = true;
                await s.QueryAsync(db => db.BranchUsers.Where(b => b.BranchId == s.BranchId && b.UserId == s.Day.Manager.User.Id).ExecuteUpdateAsync(x => x.SetProperty(b => b.Role, Role.Member)));
            }))
        {
            var replay = await client.PostAuthAsync(route, request, s.ManagerToken, key);
            hooked.ShouldBeTrue();
            replay.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await replay.Content.ReadAsStringAsync());
        }
        (await s.BranchReceiptCountAsync()).ShouldBe(1);
        (await s.BranchEventCountAsync()).ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentImportsAndLostResponseConfirmationPreserveOneEvidenceAndOneMatch(bool sharedImportKey)
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "OfxConcurrent");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var settled = await Settle(s, Batch(await Provider(s), await Card(s, 70m), 0, 70m, 0m, 70m));
        var ledgerCount = await s.BranchTransactionCountAsync();
        var importRequest = ImportRequest(s, Statement(("concurrent", 70m)));
        var key = Guid.NewGuid().ToString();
        var imports = await Task.WhenAll(
            Post<ResponseBankStatementJson>(s, "/bank-statement/import", importRequest, key),
            Post<ResponseBankStatementJson>(s, "/bank-statement/import", importRequest, sharedImportKey ? key : Guid.NewGuid().ToString()));
        imports[0].Id.ShouldBe(imports[1].Id);
        imports[0].Lines.Single().Id.ShouldBe(imports[1].Lines.Single().Id);
        (await s.QueryAsync(db => db.BankStatementImports.CountAsync(i => i.BranchId == s.BranchId))).ShouldBe(1);
        (await s.QueryAsync(db => db.BankStatementLines.CountAsync(i => i.BranchId == s.BranchId))).ShouldBe(1);
        var proposal = (await Propose(s, imports[0].Id)).Proposals.Single();
        var confirm = ConfirmRequest(proposal);
        var confirmKey = Guid.NewGuid().ToString();
        var matches = await Task.WhenAll(
            Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", confirm, confirmKey),
            Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", confirm, confirmKey));
        matches[0].MatchIds.ShouldBe(matches[1].MatchIds);
        (await s.QueryAsync(db => db.BankStatementMatches.CountAsync(i => i.BranchId == s.BranchId))).ShouldBe(1);
        (await s.ReceiptAsync(settled.BankReceipt.Id)).Status.ShouldBe(BankReceiptStatus.Confirmed);
        (await s.BranchReceiptCountAsync()).ShouldBe(1);
        (await s.BranchEventCountAsync()).ShouldBe(1);
        (await s.BranchTransactionCountAsync()).ShouldBe(ledgerCount);
    }

    [Fact]
    public async Task OneProviderIdentityCannotBeCreatedConcurrentlyOnDifferentBanks()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "ProviderIdentityRace");
        var other = await factory.SeedAccountAsync(s.BranchId, AccountType.BankAccount, "Other provider bank");
        var results = await Task.WhenAll(new[] { s.Bank.Id, other.Id }.Select(bank => client.PostAuthAsync(
            "/card-provider", new RequestCardProviderJson
            {
                Name = "Synthetic provider", ExternalIdentity = "one-source", Brand = "Synthetic",
                BankAccountId = bank, DelayDays = 1, BusinessDays = false
            }, s.ManagerToken, Guid.NewGuid().ToString())));
        results.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        (await s.QueryAsync(db => db.CardProviders.CountAsync(p => p.BranchId == s.BranchId))).ShouldBe(1);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LaterManagerFeeEvidenceResolvesUnknownOrDifference_WithoutRewritingMoney_AndInvalidatesOldProposals(bool unknown)
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "FeeResolution");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        await ConfigureBank(s);
        var settled = await Settle(s, Batch(await Provider(s), await Card(s, 50m), 0, 50m, unknown ? null : 1m, 48m));
        var before = await s.BranchTransactionCountAsync();
        var eventIds = await s.QueryAsync(db => db.SettlementEvents.Where(e => e.BranchId == s.BranchId).Select(e => e.Id).ToListAsync());
        var import = await Import(s, Statement(("resolved", 48m)));
        (await Propose(s, import.Id)).Proposals.ShouldBeEmpty();
        var request = FeeEvidence(settled, 0, 2m);
        var key = Guid.NewGuid().ToString();
        var resolved = await Post<ResponseBankReceiptJson>(s, "/card-settlement/fee-evidence", request, key);
        var replay = await Post<ResponseBankReceiptJson>(s, "/card-settlement/fee-evidence", request, key);
        resolved.Id.ShouldBe(settled.BankReceipt.Id);
        resolved.Amount.ShouldBe(unknown ? 50m : 49m);
        resolved.ExpectedAmount.ShouldBe(48m);
        resolved.ConfirmedFee.ShouldBe(2m); resolved.UnmatchedDifference.ShouldBe(0m);
        resolved.FeeUnknown.ShouldBeFalse(); resolved.FeeEvidenceVersion.ShouldBe(1);
        replay.FeeEvidenceHistory.Single().Id.ShouldBe(resolved.FeeEvidenceHistory.Single().Id);
        resolved.FeeEvidenceHistory.Single().CreatedByUserId.ShouldBe(s.Day.Manager.User.Id);
        var resultAfterFee = await FinancialResult(s);
        resultAfterFee.KnownOwnResult.ShouldBe(-2m);
        resultAfterFee.FeeAmountsComplete.ShouldBeTrue();
        var evidenceId = resolved.FeeEvidenceHistory.Single().Id;
        var tamper = await Should.ThrowAsync<Npgsql.PostgresException>(() => s.QueryAsync(db => db.CardSettlementFeeEvidence
            .Where(e => e.Id == evidenceId).ExecuteUpdateAsync(x => x.SetProperty(e => e.ConfirmedFee, 0m))));
        tamper.SqlState.ShouldBe("23514");
        var original = await s.QueryAsync(db => db.CardSettlementBatches.AsNoTracking().SingleAsync(b => b.Id == settled.Id));
        original.ConfirmedFee.ShouldBe(unknown ? null : 1m);
        var stale = (await Propose(s, import.Id)).Proposals.Single();
        await Post<ResponseBankReceiptJson>(s, "/card-settlement/fee-evidence", FeeEvidence(settled, 1, 2m));
        var refused = await client.PostAuthAsync("/bank-statement/confirm", ConfirmRequest(stale), s.ManagerToken, Guid.NewGuid().ToString());
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict, await refused.Content.ReadAsStringAsync());
        await Post<ResponseBankMatchConfirmationJson>(s, "/bank-statement/confirm", ConfirmRequest((await Propose(s, import.Id)).Proposals.Single()));
        var late = await client.PostAuthAsync("/card-settlement/fee-evidence", FeeEvidence(settled, 2, 2m), s.ManagerToken, Guid.NewGuid().ToString());
        late.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await s.ReceiptAsync(settled.BankReceipt.Id)).FeeEvidenceHistory.Count.ShouldBe(2);
        (await s.BranchTransactionCountAsync()).ShouldBe(before);
        (await s.QueryAsync(db => db.SettlementEvents.Where(e => e.BranchId == s.BranchId).Select(e => e.Id).ToListAsync())).ShouldBe(eventIds);
        (await s.QueryAsync(db => db.BankStatementMatches.CountAsync(m => m.BranchId == s.BranchId))).ShouldBe(1);
    }

    [Fact]
    public async Task ConcurrentFeeEvidenceVersionsAllowExactlyOneAppend_AndLostAuthorityCannotReplay()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "FeeRace");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var settled = await Settle(s, Batch(await Provider(s), await Card(s, 100m), 0, 100m, null, 98m));
        var request = FeeEvidence(settled, 0, 2m);
        var key = Guid.NewGuid().ToString();
        var results = await Task.WhenAll(client.PostAuthAsync("/card-settlement/fee-evidence", request, s.ManagerToken, key),
            client.PostAuthAsync("/card-settlement/fee-evidence", request, s.ManagerToken, Guid.NewGuid().ToString()));
        results.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        (await s.ReceiptAsync(settled.BankReceipt.Id)).FeeEvidenceHistory.Count.ShouldBe(1);
        await s.QueryAsync(db => db.BranchUsers.Where(b => b.BranchId == s.BranchId && b.UserId == s.Day.Manager.User.Id).ExecuteUpdateAsync(x => x.SetProperty(b => b.Role, Role.Member)));
        var denied = await client.PostAuthAsync("/card-settlement/fee-evidence", request, s.ManagerToken, key);
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ReexportFormattingAndSameCivilDateReuseOneBankLine_WhileEconomicChangeStillConflicts()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "OfxCanonical");
        await ConfigureBank(s);
        var body = Statement(("stable", 10m));
        var original = await Import(s, body);
        var pretty = "<?xml version=\"1.0\"?>" + System.Xml.Linq.XElement.Parse(body).ToString().Replace("20261006</DTPOSTED>", "20261006120000[-3:BRT]</DTPOSTED>").Replace("<TRNAMT>10", "<TRNAMT>+10");
        var reexport = await Import(s, pretty);
        original.Lines.Single().Id.ShouldBe(reexport.Lines.Single().Id);
        (await s.QueryAsync(db => db.BankStatementLines.CountAsync(l => l.BranchId == s.BranchId))).ShouldBe(1);
        var changed = await client.PostAuthAsync("/bank-statement/import", ImportRequest(s, body.Replace("<TRNAMT>10", "<TRNAMT>11")), s.ManagerToken, Guid.NewGuid().ToString());
        changed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await s.QueryAsync(db => db.BankStatementImports.CountAsync(i => i.BranchId == s.BranchId))).ShouldBe(2);
        // An earlier importer retained an XML-based fingerprint. Reuse that immutable line semantically.
        var legacyOriginal = "<STMTTRN><TRNTYPE>CREDIT</TRNTYPE><DTPOSTED>20261006</DTPOSTED><TRNAMT>15.00</TRNAMT><FITID>legacy</FITID></STMTTRN>";
        var legacyHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(legacyOriginal)));
        var legacy = new BankStatementLine
        {
            BranchId = s.BranchId, BankAccountId = s.Bank.Id, BankIdentity = "legacy", Fingerprint = legacyHash,
            Original = legacyOriginal, Amount = 15m, PostedOn = new DateTime(2026, 10, 6), Type = "CREDIT"
        };
        await s.QueryAsync(async db => { db.BankStatementLines.Add(legacy); return await db.SaveChangesAsync(); });
        var legacyImport = await Import(s, Statement(("legacy", 15m)));
        legacyImport.Lines.Single().Id.ShouldBe(legacy.Id);
        (await s.QueryAsync(db => db.BankStatementLines.AsNoTracking().SingleAsync(l => l.Id == legacy.Id))).Fingerprint.ShouldBe(legacyHash);
    }

    [Fact]
    public async Task CollectionRefusesDrawerSafeSystemTypesInsteadOfMislabelingTheBankLedger()
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "WrongSystemCollection");
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var response = await client.PostAuthAsync("/bank-movement", new RequestBankMovementJson
        {
            Kind = BankMovementKind.SafeCollection, BankAccountId = s.Bank.Id, SafeAccountId = s.Day.SafeId,
            BankTransactionTypeId = s.Day.SeededTypes["sistema.cofre-deposito"].Id, SourceTransactionTypeId = s.Day.SeededTypes["saidas.deposito-dinheiro"].Id,
            RecordedByOperatorId = s.Day.MemberA.Operator!.Id, Amount = 100m, OccurredOn = new DateTime(2026, 10, 5), Evidence = "Wrong system types"
        }, s.ManagerToken, Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await s.BranchTransactionCountAsync()).ShouldBe(0);
        (await s.BranchReceiptCountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData("fee")]
    [InlineData("receipt-version")]
    [InlineData("evidence-version")]
    [InlineData("locked")]
    public async Task InvalidFeeEvidenceLeavesOriginalExpectationAndNoHistory(string invalid)
    {
        var s = await ChequeScenario.CreateAsync(factory, client, "InvalidFee" + invalid);
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 10, 6, 10, 0, 0));
        var settled = await Settle(s, Batch(await Provider(s), await Card(s, 50m), 0, 50m, null, 48m));
        if (invalid == "locked") await s.LockThroughAsync(new DateTime(2026, 10, 5));
        var request = new RequestCardFeeEvidenceJson
        {
            BatchId = settled.Id, ReceiptVersion = settled.BankReceipt.Version + (invalid == "receipt-version" ? 1u : 0u),
            ExpectedFeeEvidenceVersion = invalid == "evidence-version" ? 1 : 0,
            ConfirmedFee = invalid == "fee" ? 50m : 2m, Basis = "Explicit", Evidence = "Synthetic", Reason = "Audit",
            OccurredOn = new DateTime(2026, 10, 5), PostedOn = invalid == "locked" ? new DateTime(2026, 10, 5) : null
        };
        var failed = await client.PostAuthAsync("/card-settlement/fee-evidence", request, s.ManagerToken, Guid.NewGuid().ToString());
        failed.StatusCode.ShouldBe(HttpStatusCode.Conflict, await failed.Content.ReadAsStringAsync());
        var receipt = await s.ReceiptAsync(settled.BankReceipt.Id);
        receipt.FeeUnknown.ShouldBeTrue(); receipt.ExpectedAmount.ShouldBe(50m); receipt.FeeEvidenceHistory.ShouldBeEmpty();
        (await s.BranchEventCountAsync()).ShouldBe(1);
    }

    private static RequestCardFeeEvidenceJson FeeEvidence(ResponseCardSettlementJson batch, int version, decimal fee) => new()
    {
        BatchId = batch.Id, ReceiptVersion = batch.BankReceipt.Version, ExpectedFeeEvidenceVersion = version,
        ConfirmedFee = fee, Basis = "Tarifa explicitamente confirmada", Evidence = "Comprovante sintético", Reason = "Conferência posterior",
        OccurredOn = new DateTime(2026, 10, 6)
    };

    private async Task ConfigureBank(ChequeScenario s)
    {
        await s.QueryAsync(db => db.Accounts.Where(a => a.Id == s.Bank.Id).ExecuteUpdateAsync(x => x.SetProperty(a => a.Number, "Conta de exibição").SetProperty(a => a.Institution, "Banco sintético")));
        var binding = await Post<ResponseBankStatementAccountBindingJson>(s, "/bank-statement/account-binding", new RequestBankStatementAccountBindingJson
        {
            BankAccountId = s.Bank.Id, BankIdentity = "104", AccountIdentity = "fake-account"
        });
        accountBindings[s.Bank.Id] = binding.Id;
    }
    private async Task<Transaction> Card(ChequeScenario s, decimal amount)
    {
        var type = s.Day.SeededTypes["saidas.cartao-credito"];
        var row = await factory.SeedTransactionAsync(s.BranchId, s.Day.CashboxId, type.Id, type.CategoryId, Direction.Out, s.Day.MemberA.Operator!.Id, s.Day.Manager.User.Id, new DateTime(2026, 9, 25), amount);
        return (await factory.ReloadAsync<Transaction>(row.Id))!;
    }
    private Task<ResponseCardProviderJson> Provider(ChequeScenario s) => Post<ResponseCardProviderJson>(s, "/card-provider", new RequestCardProviderJson { Name = "Sintético", ExternalIdentity = "fake-provider", Brand = "Teste", BankAccountId = s.Bank.Id, DelayDays = 2, BusinessDays = true });
    private static RequestCardSettlementJson Batch(
        ResponseCardProviderJson p,
        Transaction row,
        int version,
        decimal gross,
        decimal? fee,
        decimal observed) => new()
    {
        ProviderId = p.Id, ProviderVersion = p.Version, ExternalIdentity = Guid.NewGuid().ToString(),
        OccurredOn = new DateTime(2026, 10, 5), PlanningBasisOn = new DateTime(2026, 10, 2),
        ConfirmedFee = fee, ManagerConfirmedFee = fee is not null, FeeBasis = fee is null ? " " : "Valor explicitamente conferido", FeeEvidence = fee is null ? "\t" : "Comprovante sintético", ObservedNetAmount = observed,
        Items = [new() { TransactionId = row.Id, TransactionVersion = row.Version, ExpectedVersion = version, GrossAmount = gross }]
    };
    private Task<ResponseCardSettlementJson> Settle(ChequeScenario s, RequestCardSettlementJson r) => Post<ResponseCardSettlementJson>(s, "/card-settlement", r);
    private Task<ResponseBankStatementJson> Import(ChequeScenario s, string body) => Post<ResponseBankStatementJson>(s, "/bank-statement/import", ImportRequest(s, body));
    private RequestBankStatementImportJson ImportRequest(ChequeScenario s, string body) => new() { AccountBindingId = accountBindings[s.Bank.Id], BankAccountId = s.Bank.Id, BankIdentity = "104", AccountIdentity = "fake-account", ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(body)) };
    private Task<ResponseBankMatchProposalsJson> Propose(ChequeScenario s, Guid import) => Post<ResponseBankMatchProposalsJson>(s, "/bank-statement/propose", new RequestBankMatchProposeJson { ImportId = import });
    private static RequestBankMatchConfirmJson ConfirmRequest(ResponseBankMatchProposalJson p) => new() { PostedOn = new DateTime(2026, 10, 6), Items = [new() { ProposalId = p.Id, LineId = p.LineId, LineVersion = p.LineVersion, ReceiptId = p.ReceiptId, ReceiptVersion = p.ReceiptVersion, Amount = p.Amount, ExpectationFingerprint = p.ExpectationFingerprint }] };
    private async Task<T> Post<T>(ChequeScenario s, string route, object request, string? key = null) where T : class
    {
        var response = await client.PostAuthAsync(route, request, s.ManagerToken, key ?? Guid.NewGuid().ToString());
        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<T>();
    }
    private static string Statement(params (string Id, decimal Amount)[] rows) => "<?xml version=\"1.0\"?><OFX><BANKMSGSRSV1><STMTTRNRS><STMTRS><CURDEF>BRL</CURDEF><BANKACCTFROM><BANKID>104</BANKID><ACCTID>fake-account</ACCTID></BANKACCTFROM><BANKTRANLIST><DTSTART>20261001</DTSTART><DTEND>20261031</DTEND>" + string.Concat(rows.Select(r => $"<STMTTRN><TRNTYPE>CREDIT</TRNTYPE><DTPOSTED>20261006</DTPOSTED><TRNAMT>{r.Amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}</TRNAMT><FITID>{r.Id}</FITID></STMTTRN>")) + "</BANKTRANLIST></STMTRS></STMTTRNRS></BANKMSGSRSV1></OFX>";
}
