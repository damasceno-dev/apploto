using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;
using Xunit;

namespace WebApi.Test.People;

[Collection(ServerApiCollection.Name)]
public class PeopleControllerTemporalEvidenceTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();
    private static readonly DateTime Sep = new(2026, 9, 1);

    [Fact]
    public async Task UnresolvedBranchCustody_BlocksEveryCommissionWithoutAssigningLegacyLiability()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 11, 6, 10, 0, 0));
        var s = await PeopleTestScenario.Seed(factory);
        await s.Create<ResponseCommissionRuleJson>(client, "commission-rule", PeopleRequestBuilder.Rule(s.Person.Id));
        var account = await factory.SeedAccountAsync(s.BranchId, AccountType.Terminal);
        var unknown = await factory.SeedDailyCloseAsync(s.BranchId, account.Id, Sep.AddDays(9), DailyCloseStatus.Approved, mappingState: SessionMappingState.Unresolved);
        var report = await s.Statement(client);
        report.SessionCount.ShouldBe(0);
        report.CustodyHistoryComplete.ShouldBeFalse();
        report.UnresolvedCustodySessionIds.ShouldBe([unknown.Id]);
        report.Commission.Completeness.ShouldBe(PeopleSourceCompleteness.Incomplete);
        report.Commission.ProposedAmount.ShouldBeNull(); report.Commission.CalculationId.ShouldBeNull();
        report.RecordedPaySignedTotal.ShouldBe(0);
        var category = await s.Create<ResponsePayCategoryJson>(client, "pay-category", PeopleRequestBuilder.Category());
        var request = PeopleRequestBuilder.Pay(s.Person.Id, category.Id, 1); request.CommissionCalculationId = "unavailable";
        (await client.PostAuthAsync("/people/pay-item", request, s.Token, Guid.NewGuid().ToString("N"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var other = await PeopleTestScenario.Seed(factory);
        await other.Create<ResponseCommissionRuleJson>(client, "commission-rule", PeopleRequestBuilder.Rule(other.Person.Id));
        (await other.Statement(client)).Commission.ProposedAmount.ShouldBe(0);
    }

    [Fact]
    public async Task LockedRule_CanSurrenderOnlyItsOpenFutureTail_ThenNewRateStartsWithoutOverlap()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 11, 6, 10, 0, 0));
        var s = await PeopleTestScenario.Seed(factory);
        var request = PeopleRequestBuilder.Rule(s.Person.Id); request.EffectiveTo = new DateTime(2026, 12, 31);
        var original = await s.Create<ResponseCommissionRuleJson>(client, "commission-rule", request);
        var old = await s.Statement(client, extra: "&asOfDate=2026-09-30");
        await LockSeptember(s.BranchId);
        request.PreviousId = original.Id; request.PostedOn = new DateTime(2026, 11, 1); request.EffectiveTo = new DateTime(2026, 10, 31);
        request.RatePercent = 19;
        (await client.PostAuthAsync($"/people/commission-rule/{original.Id}/revision", request, s.Token, Guid.NewGuid().ToString("N"), original.Version)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        request.RatePercent = 18.5m;
        var amendment = await client.PostAuthAsync($"/people/commission-rule/{original.Id}/revision", request, s.Token, Guid.NewGuid().ToString("N"), original.Version);
        amendment.StatusCode.ShouldBe(HttpStatusCode.Created, await amendment.Content.ReadAsStringAsync());
        var next = PeopleRequestBuilder.Rule(s.Person.Id, 20); next.EffectiveFrom = next.PostedOn = new DateTime(2026, 11, 1); next.EffectiveTo = new DateTime(2026, 12, 31);
        await s.Create<ResponseCommissionRuleJson>(client, "commission-rule", next);
        (await s.Statement(client, 11)).Commission.Rules.Single().RatePercent.ShouldBe(20);
        (await s.Statement(client, extra: "&asOfDate=2026-09-30")).Commission.CalculationId.ShouldBe(old.Commission.CalculationId);
        (await factory.ReloadAsync<EmployeeCommissionRule>(original.Id))!.EffectiveTo.ShouldBe(new DateTime(2026, 12, 31));
    }

    [Fact]
    public async Task Database_RejectsSecondCommissionRootForSamePersonMonth_EvenWithDifferentHash()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 11, 6, 10, 0, 0));
        var s = await PeopleTestScenario.Seed(factory);
        var category = await s.Create<ResponsePayCategoryJson>(client, "pay-category", PeopleRequestBuilder.Category());
        EmployeePayItem Row(string hash) => new()
        {
            BranchId = s.BranchId,
            ActorUserId = s.UserId,
            OperatorId = s.Person.Id,
            CategoryId = category.Id,
            CategoryName = category.Name,
            SourceMonth = Sep,
            SourceDate = Sep,
            PostedOn = Sep,
            Kind = PayItemKind.Addition,
            Amount = 10,
            SignedContribution = 10,
            Reason = "Explicit synthetic SQL proof",
            Evidence = "Independent unique-index assertion",
            CommissionCalculationId = hash,
            CommissionEvidenceJson = "{}"
        };
        await factory.ReadDb(async db => { db.EmployeePayItems.Add(Row("first-cutoff")); await db.SaveChangesAsync(); return 0; });
        var failure = await Should.ThrowAsync<DbUpdateException>(() => factory.ReadDb(async db => { db.EmployeePayItems.Add(Row("different-cutoff")); await db.SaveChangesAsync(); return 0; }));
        var postgres = failure.InnerException.ShouldBeOfType<PostgresException>();
        postgres.SqlState.ShouldBe("23505"); postgres.ConstraintName.ShouldBe("IX_EmployeePayItems_BranchId_OperatorId_SourceMonth");
    }

    [Fact]
    public async Task OfficialFeeCorrections_PreserveEarlierCutoff_ExposeMismatch_AndPostOnlyTheDeliberatePayDelta()
    {
        var day = Sep.AddDays(9);
        using var clock = factory.FreezeBranchLocalTime(day.AddHours(10));
        var s = await FinancialDayScenario.CreateAsync(factory, client, "CommissionPosting", businessDate: day);
        async Task<T> Create<T>(string route, object body) where T : class
        {
            var response = await client.PostAuthAsync("/people/" + route, body, s.Manager.Token, Guid.NewGuid().ToString("N"));
            response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync()); return await response.ReadContentAsync<T>();
        }
        async Task<ResponseEmployeeStatementJson> Read(int month = 9, string extra = "") => await (await client.GetAuthAsync($"/report/employee-statement?operatorId={s.MemberA.Operator!.Id}&year=2026&month={month}" + extra, s.Manager.Token)).ReadContentAsync<ResponseEmployeeStatementJson>();
        var rule = PeopleRequestBuilder.Rule(s.MemberA.Operator!.Id); rule.EffectiveTo = new DateTime(2026, 10, 31);
        await Create<ResponseCommissionRuleJson>("commission-rule", rule);
        var category = await Create<ResponsePayCategoryJson>("pay-category", PeopleRequestBuilder.Category());
        var session = await s.StartAsync();
        await s.SaveOkAsync(session, 1100, fields: FinancialDayScenario.Fields(bolao: 100)); await client.ConferAsync(session, s.MemberA.Token); await client.SubmitAsync(session, s.MemberA.Token);
        var first = await Read(); first.Commission.ProposedAmount.ShouldBe(18.5m);
        var pay = PeopleRequestBuilder.Pay(s.MemberA.Operator.Id, category.Id, 18.5m); pay.PostedOn = day; pay.CommissionCalculationId = first.Commission.CalculationId;
        var original = await Create<ResponsePayItemJson>("pay-item", pay);
        clock.AdvanceTo(day.AddDays(1).AddHours(10));
        (await client.PostAuthAsync($"/session/{session}/recall", s.Manager.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await s.SaveOkAsync(session, 1200, fields: FinancialDayScenario.Fields(bolao: 200), actor: s.Manager); await client.ConferAsync(session, s.Manager.Token); await client.SubmitAsync(session, s.Manager.Token);
        var changed = await Read(); changed.Commission.ProposedAmount.ShouldBe(37); changed.Commission.RecordedSourceMonthAmount.ShouldBe(18.5m);
        changed.Commission.RequiresReconciliation.ShouldBeTrue(); changed.Commission.DifferenceFromRecorded.ShouldBe(18.5m);
        (await Read(extra: "&asOfDate=2026-09-10")).Commission.ProposedAmount.ShouldBe(18.5m);
        pay.PreviousId = original.Id; pay.Amount = 37; pay.PostedOn = day.AddDays(1); pay.CommissionCalculationId = changed.Commission.CalculationId;
        var key = Guid.NewGuid().ToString("N"); var endpoint = $"/people/pay-item/{original.Id}/revision";
        var revised = await client.PostAuthAsync(endpoint, pay, s.Manager.Token, key, original.Version);
        revised.StatusCode.ShouldBe(HttpStatusCode.Created, await revised.Content.ReadAsStringAsync());
        var result = await revised.ReadContentAsync<ResponsePayItemJson>(); result.SignedContribution.ShouldBe(18.5m);
        (await (await client.PostAuthAsync(endpoint, pay, s.Manager.Token, key, original.Version)).ReadContentAsync<ResponsePayItemJson>()).Id.ShouldBe(result.Id);
        var reconciled = await Read(); reconciled.RecordedPaySignedTotal.ShouldBe(37); reconciled.Commission.RequiresReconciliation.ShouldBeFalse();
        clock.AdvanceTo(new DateTime(2026, 10, 2, 10, 0, 0));
        (await client.PostAuthAsync($"/session/{session}/recall", s.Manager.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await s.SaveOkAsync(session, 1300, fields: FinancialDayScenario.Fields(bolao: 300), actor: s.Manager); await client.ConferAsync(session, s.Manager.Token); await client.SubmitAsync(session, s.Manager.Token);
        var october = await Read(10); october.Commission.ProposedAmount.ShouldBe(18.5m); october.Commission.KnownBasis.ShouldBe(100);
        october.Commission.Basis.ShouldAllBe(b => b.SourceDate == day && b.PostedOn == new DateTime(2026, 10, 2));
        (await Read()).Commission.ProposedAmount.ShouldBe(37);
        (await factory.ReloadAsync<EmployeePayItem>(original.Id))!.Amount.ShouldBe(18.5m);
        await factory.ReadDb(async db => { (await db.EmployeeCommissionFeePostings.Where(p => p.SessionId == session).SumAsync(p => p.SignedAmount)).ShouldBe(300); (await db.TimeEntries.CountAsync(p => p.BranchId == s.Branch.Id)).ShouldBe(0); return 0; });
    }

    [Fact]
    public async Task DrawRevisionChain_HasOneRootTotal_AndLaterCancellationPostsInItsOwnMonth()
    {
        var day = Sep.AddDays(9);
        using var clock = factory.FreezeBranchLocalTime(day.AddHours(10));
        var s = await FinancialDayScenario.CreateAsync(factory, client, "DrawChainPosting", businessDate: day);
        var session = await s.StartAsync();
        await s.SaveOkAsync(session, 1015, fields: FinancialDayScenario.Fields(special: 15)); await client.ConferAsync(session, s.MemberA.Token); await client.SubmitAsync(session, s.MemberA.Token);
        async Task<T> Create<T>(string route, object body) where T : class
        {
            var response = await client.PostAuthAsync("/people/" + route, body, s.Manager.Token, Guid.NewGuid().ToString("N"));
            response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync()); return await response.ReadContentAsync<T>();
        }
        async Task<ResponseEmployeeStatementJson> Read(int month) => await (await client.GetAuthAsync($"/report/employee-statement?operatorId={s.MemberA.Operator!.Id}&year=2026&month={month}", s.Manager.Token)).ReadContentAsync<ResponseEmployeeStatementJson>();
        var effect = await factory.ReadDb(db => db.SessionFieldEffects.AsNoTracking().SingleAsync(e => e.SessionId == session && e.Field == SessionCloseField.SpecialFee));
        var request = new RequestSpecialDrawJson { Name = "Original synthetic draw", Modality = "Explicit", Contest = "123", PeriodFrom = Sep, PeriodTo = new DateTime(2026, 9, 30), Prize = 1000, PostedOn = day, Reason = "Explicit catalog", Evidence = "Synthetic source" };
        var root = await Create<ResponseSpecialDrawJson>("special-draw", request);
        var allocation = new RequestSpecialDrawAllocationJson { DrawId = root.Id, SessionId = session, SessionFieldEffectId = effect.Id, SourceRevision = effect.Revision, SourceDate = day, PostedOn = day, Amount = 8, Reason = "Explicit allocation", Evidence = "Synthetic sale evidence" };
        var first = await Create<ResponseSpecialDrawAllocationJson>("special-draw-allocation", allocation);
        clock.AdvanceTo(day.AddDays(1).AddHours(10));
        request.PreviousId = root.Id; request.PostedOn = day.AddDays(1); request.Name = "Revised synthetic draw";
        var revised = await client.PostAuthAsync($"/people/special-draw/{root.Id}/revision", request, s.Manager.Token, Guid.NewGuid().ToString("N"), root.Version);
        revised.StatusCode.ShouldBe(HttpStatusCode.Created, await revised.Content.ReadAsStringAsync());
        var head = await revised.ReadContentAsync<ResponseSpecialDrawJson>();
        var nextAllocation = new RequestSpecialDrawAllocationJson { DrawId = head.Id, SessionId = session, SessionFieldEffectId = effect.Id, SourceRevision = effect.Revision, SourceDate = day, PostedOn = day.AddDays(1), Amount = 4, Reason = "Second explicit allocation", Evidence = "Second sale evidence" };
        await Create<ResponseSpecialDrawAllocationJson>("special-draw-allocation", nextAllocation);
        var september = await Read(9); september.SpecialDraws.Draws.Single().DrawId.ShouldBe(root.Id); september.SpecialDraws.Draws.Single().AllocatedFee.ShouldBe(12);
        // A real official fee correction changes the source revision; allocation cancellation retains its frozen revision.
        (await client.PostAuthAsync($"/session/{session}/recall", s.Manager.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await s.SaveOkAsync(session, 1010, fields: FinancialDayScenario.Fields(special: 10), actor: s.Manager);
        await client.ConferAsync(session, s.Manager.Token); await client.SubmitAsync(session, s.Manager.Token);
        (await Read(9)).SpecialDraws.SourceMappingComplete.ShouldBeFalse();
        await LockSeptember(s.Branch.Id); clock.AdvanceTo(new DateTime(2026, 10, 2, 10, 0, 0));
        request.PreviousId = head.Id; request.PostedOn = new DateTime(2026, 10, 2); request.Name = "Forbidden changed locked identity";
        (await client.PostAuthAsync($"/people/special-draw/{head.Id}/revision", request, s.Manager.Token, Guid.NewGuid().ToString("N"), head.Version)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        allocation.PreviousId = first.Id; allocation.Cancelled = true; allocation.PostedOn = new DateTime(2026, 10, 2);
        var cancel = await client.PostAuthAsync($"/people/special-draw-allocation/{first.Id}/revision", allocation, s.Manager.Token, Guid.NewGuid().ToString("N"), first.Version);
        cancel.StatusCode.ShouldBe(HttpStatusCode.Created, await cancel.Content.ReadAsStringAsync());
        (await Read(9)).SpecialDraws.AllocatedFeeTotal.ShouldBe(12);
        var october = await Read(10); october.SpecialDraws.AllocatedFeeTotal.ShouldBe(-8); october.SpecialDraws.SourceFeeTotal.ShouldBe(0);
        october.SpecialDraws.Draws.Single().DrawId.ShouldBe(root.Id); october.SpecialDraws.Draws.Single().AllocatedFee.ShouldBe(-8);
        (await factory.ReloadAsync<SessionFieldEffect>(effect.Id))!.Value.ShouldBe(10);
    }

    [Fact]
    public async Task FailedCommissionPublication_RollsBackOfficialSubmission_AndRetryPublishesExactlyOnce()
    {
        var day = Sep.AddDays(9);
        using var clock = factory.FreezeBranchLocalTime(day.AddHours(10));
        var s = await FinancialDayScenario.CreateAsync(factory, client, "CommissionAtomic", businessDate: day);
        var session = await s.StartAsync();
        await s.SaveOkAsync(session, 1100, fields: FinancialDayScenario.Fields(bolao: 100)); await client.ConferAsync(session, s.MemberA.Token);
        var trigger = "m98_fee_fail_" + Guid.NewGuid().ToString("N");
        await factory.ReadDb(async db =>
        {
#pragma warning disable EF1002
            return await db.Database.ExecuteSqlRawAsync($"""
                CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."BranchId"='{s.Branch.Id}'::uuid THEN RAISE EXCEPTION 'M98_SYNTHETIC_PUBLICATION_FAILURE'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER {trigger} BEFORE INSERT ON "EmployeeCommissionFeePostings" FOR EACH ROW EXECUTE FUNCTION {trigger}();
                """);
#pragma warning restore EF1002
        });
        try
        {
            (await client.SubmitSessionAsync(session, s.MemberA.Token)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            (await factory.ReloadAsync<DailyClose>(session))!.Status.ShouldBe(DailyCloseStatus.Draft);
            await factory.ReadDb(async db => { (await db.EmployeeCommissionFeePostings.CountAsync(p => p.SessionId == session)).ShouldBe(0); return 0; });
        }
        finally
        {
            await factory.ReadDb(async db =>
            {
#pragma warning disable EF1002
                return await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER {trigger} ON \"EmployeeCommissionFeePostings\"; DROP FUNCTION {trigger}();");
#pragma warning restore EF1002
            });
        }
        await client.SubmitAsync(session, s.MemberA.Token);
        await factory.ReadDb(async db =>
        {
            (await db.EmployeeCommissionFeePostings.CountAsync(p => p.SessionId == session)).ShouldBe(1);
            var row = await db.EmployeeCommissionFeePostings.SingleAsync(p => p.SessionId == session);
            row.SignedAmount.ShouldBe(100);
            var failure = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"EmployeeCommissionFeePostings\" SET \"SignedAmount\"=99 WHERE \"Id\"={row.Id}"));
            failure.ConstraintName.ShouldBe("CK_People_Immutable");
            return 0;
        });
    }

    [Fact]
    public async Task ExplicitAccessFormula_OnSeededSources_ReconcilesWithoutAutomaticPayOrDeduction()
    {
        var day = Sep.AddDays(9);
        using var clock = factory.FreezeBranchLocalTime(day.AddHours(10));
        var s = await FinancialDayScenario.CreateAsync(factory, client, "AccessPeopleGolden", businessDate: day);
        async Task<T> Create<T>(string route, object body) where T : class
        {
            var response = await client.PostAuthAsync("/people/" + route, body, s.Manager.Token, Guid.NewGuid().ToString("N"));
            response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync()); return await response.ReadContentAsync<T>();
        }
        async Task<ResponseEmployeeStatementJson> Read() => await (await client.GetAuthAsync($"/report/employee-statement?operatorId={s.MemberA.Operator!.Id}&year=2026&month=9", s.Manager.Token)).ReadContentAsync<ResponseEmployeeStatementJson>();
        var rule = PeopleRequestBuilder.Rule(s.MemberA.Operator!.Id); rule.IncludeExplicitSources = true;
        await Create<ResponseCommissionRuleJson>("commission-rule", rule);
        var session = await s.StartAsync();
        await s.SaveOkAsync(session, 1115, fields: FinancialDayScenario.Fields(bolao: 100, special: 15));
        await client.ConferAsync(session, s.MemberA.Token); await client.SubmitAsync(session, s.MemberA.Token);
        foreach (var (role, value) in new[] { ("BolaoLeftover", 5m), ("EnteredPrize", 20m) })
            await Create<ResponseCommissionSourceJson>("commission-source", new RequestCommissionSourceJson
            {
                OperatorId = s.MemberA.Operator.Id,
                SourceDate = day,
                PostedOn = day,
                SourceRole = role,
                SignedAmount = value,
                ExternalIdentity = Guid.NewGuid().ToString("N"),
                Reason = "Explicit synthetic Access formula input",
                Evidence = "Seeded independent input; no inferred allocation"
            });
        var additions = await Create<ResponsePayCategoryJson>("pay-category", PeopleRequestBuilder.Category());
        var deductions = await Create<ResponsePayCategoryJson>("pay-category", PeopleRequestBuilder.Category(PayItemKind.Deduction));
        var salary = PeopleRequestBuilder.Pay(s.MemberA.Operator.Id, additions.Id, 1000); salary.PostedOn = day;
        var deliberate = PeopleRequestBuilder.Pay(s.MemberA.Operator.Id, deductions.Id, 50, PayItemKind.Deduction); deliberate.PostedOn = day;
        await Create<ResponsePayItemJson>("pay-item", salary); await Create<ResponsePayItemJson>("pay-item", deliberate);
        // Independent golden: (100 + 15 + 5 + 20) × 18.5% = 25.90; 1000 - 50 + 25.90 = 975.90.
        var proposal = await Read(); proposal.Commission.BasisSourceCount.ShouldBe(4); proposal.Commission.KnownBasis.ShouldBe(140);
        proposal.Commission.ProposedAmount.ShouldBe(25.90m); proposal.RecordedPaySignedTotal.ShouldBe(950);
        var before = await s.RowsAsync();
        var commission = PeopleRequestBuilder.Pay(s.MemberA.Operator.Id, additions.Id, 25.90m); commission.PostedOn = day; commission.CommissionCalculationId = proposal.Commission.CalculationId;
        await Create<ResponsePayItemJson>("pay-item", commission);
        var recorded = await Read(); recorded.RecordedPaySignedTotal.ShouldBe(975.90m); recorded.Commission.DifferenceFromRecorded.ShouldBe(0);
        recorded.AutomaticPayrollDeduction.ShouldBeFalse(); recorded.RepresentsPayment.ShouldBeFalse();
        (await s.RowsAsync()).Select(r => (r.Id, r.Value, r.Status)).ShouldBe(before.Select(r => (r.Id, r.Value, r.Status)));
        await factory.ReadDb(async db => { (await db.TimeEntries.CountAsync(r => r.BranchId == s.Branch.Id)).ShouldBe(0); return 0; });
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task ExplicitReclassification_BlocksDivergentCommission_UntilOfficialResubmission(bool startsAsFee, bool changesFeeRole)
    {
        var day = Sep.AddDays(9);
        using var clock = factory.FreezeBranchLocalTime(day.AddHours(10));
        var s = await FinancialDayScenario.CreateAsync(factory, client, "CommissionClassification", businessDate: day);
        var person = s.MemberA.Operator!.Id;
        var rule = PeopleRequestBuilder.Rule(person); rule.IncludeSpecialFee = false;
        var configured = await client.PostAuthAsync("/people/commission-rule", rule, s.Manager.Token, Guid.NewGuid().ToString("N"));
        configured.StatusCode.ShouldBe(HttpStatusCode.Created, await configured.Content.ReadAsStringAsync());
        var session = await s.StartAsync();
        var category = await factory.SeedCategoryAsync(s.Branch.Id, defaultDirection: Direction.In);
        var type = await factory.SeedTransactionTypeAsync(category.Id,
            monthlyFinancialRole: startsAsFee ? MonthlyFinancialRole.BolaoServiceFee : MonthlyFinancialRole.None,
            financialClass: startsAsFee ? FinancialClass.ShopFeesAndIncome : FinancialClass.CaixaAndClientMoney);
        var transaction = await factory.SeedTransactionAsync(s.Branch.Id, s.CashboxId, type.Id, category.Id, Direction.In,
            person, s.Manager.User.Id, date: day, value: 100);
        await s.SaveOkAsync(session, 1100);
        await client.ConferAsync(session, s.MemberA.Token); await client.SubmitAsync(session, s.MemberA.Token);
        async Task<ResponseEmployeeStatementJson> Read(string extra = "") => await (await client.GetAuthAsync($"/report/employee-statement?operatorId={person}&year=2026&month=9" + extra, s.Manager.Token)).ReadContentAsync<ResponseEmployeeStatementJson>();
        var before = await Read(); before.Commission.ProposedAmount.ShouldBe(startsAsFee ? 18.5m : 0m);
        var originalRows = await factory.ReadDb(db => db.EmployeeCommissionFeePostings.Where(p => p.SessionId == session).OrderBy(p => p.Id).Select(p => new { p.Id, p.SignedAmount, p.PostedOn, p.ClassificationRevision }).ToListAsync());
        clock.AdvanceTo(day.AddDays(1).AddHours(10));
        var targetClass = !startsAsFee || changesFeeRole ? FinancialClass.ShopFeesAndIncome : FinancialClass.CaixaAndClientMoney;
        var targetRole = !startsAsFee ? MonthlyFinancialRole.BolaoServiceFee : changesFeeRole ? MonthlyFinancialRole.SpecialDrawServiceFee : MonthlyFinancialRole.None;
        var edit = await client.PutAuthAsync($"/transaction-type/{type.Id}", new RequestUpdateTransactionTypeJson
        {
            Name = type.Name,
            SettlementRule = type.SettlementRule,
            RequiresTabAccountAndClient = type.RequiresTabAccountAndClient,
            SettlementInstrument = type.SettlementInstrument,
            FinancialClass = targetClass,
            MonthlyFinancialRole = targetRole
        }, s.Manager.Token);
        edit.StatusCode.ShouldBe(HttpStatusCode.OK, await edit.Content.ReadAsStringAsync());
        var row = await factory.ReloadAsync<Transaction>(transaction.Id);
        var reclassify = await client.PostAuthAsync($"/transaction-type/{type.Id}/reclassify", new RequestReclassifyTransactionTypeJson
        {
            DateFrom = day,
            DateTo = day,
            Reason = "Explicit synthetic classification correction",
            Rows = [new RequestReclassifyRowJson { TransactionId = transaction.Id, Version = row!.Version }]
        }, s.Manager.Token);
        reclassify.StatusCode.ShouldBe(HttpStatusCode.OK, await reclassify.Content.ReadAsStringAsync());
        (await client.FinancialResultAsync(s.Manager.Token, Sep)).Entries.Single(e => e.TransactionId == transaction.Id).FinancialClass.ShouldBe(targetClass);
        if (!startsAsFee)
        {
            var officialAttempt = (await factory.ReloadAsync<DailyClose>(session))!.SubmittedAttemptId!.Value;
            foreach (var invalidRevision in new int?[] { null, 999 })
            {
                var invalid = await Should.ThrowAsync<DbUpdateException>(() => factory.ReadDb(async db =>
                {
                    db.EmployeeCommissionFeePostings.Add(new EmployeeCommissionFeePosting
                    {
                        BranchId = s.Branch.Id, OperatorId = person, SessionId = session, AttemptId = officialAttempt,
                        TransactionId = transaction.Id, SourceDate = day, PostedOn = day.AddDays(1),
                        Component = CommissionBasisComponent.BolaoServiceFee, SignedAmount = 100,
                        ClassificationProvenance = ClassificationProvenance.OpenPeriodReclassification,
                        ClassificationRevision = invalidRevision
                    });
                    await db.SaveChangesAsync(); return 0;
                }));
                invalid.InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("CK_People_Source");
            }
        }
        var divergent = await Read();
        divergent.Commission.Completeness.ShouldBe(PeopleSourceCompleteness.Incomplete);
        divergent.Commission.ProposedAmount.ShouldBeNull(); divergent.Commission.CalculationId.ShouldBeNull();
        divergent.Commission.MissingInputs.ShouldContain(m => m.Contains(transaction.Id.ToString()) && m.Contains(session.ToString()) && m.Contains("Ressubmeter"));
        (await factory.ReadDb(db => db.EmployeeCommissionFeePostings.Where(p => p.SessionId == session).OrderBy(p => p.Id).Select(p => new { p.Id, p.SignedAmount, p.PostedOn, p.ClassificationRevision }).ToListAsync())).ShouldBe(originalRows);
        if (startsAsFee) (await Read("&asOfDate=2026-09-10")).Commission.CalculationId.ShouldBe(before.Commission.CalculationId);
        clock.AdvanceTo(day.AddDays(2).AddHours(10));
        (await client.PostAuthAsync($"/session/{session}/recall", s.Manager.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await s.SaveOkAsync(session, 1100, actor: s.Manager);
        await client.ConferAsync(session, s.Manager.Token); await client.SubmitAsync(session, s.Manager.Token);
        var published = await Read(); published.Commission.Completeness.ShouldBe(PeopleSourceCompleteness.Complete);
        published.Commission.ProposedAmount.ShouldBe(startsAsFee ? 0m : 18.5m);
        published.Commission.MissingInputs.ShouldBeEmpty();
        await factory.ReadDb(async db =>
        {
            var publications = await db.EmployeeCommissionFeePostings.AsNoTracking().Where(p => p.SessionId == session).ToListAsync();
            publications.Where(p => p.PostedOn == day.AddDays(2)).ShouldAllBe(p => p.ClassificationRevision == 2);
            publications.Where(p => p.PostedOn == day.AddDays(2)).ShouldNotBeEmpty();
            var official = await db.DailyCloses.AsNoTracking().SingleAsync(c => c.Id == session);
            // Classification did not change cash/count/ledger arithmetic; the valid count attempt is reused.
            publications.ShouldAllBe(p => p.AttemptId == official.SubmittedAttemptId);
            return 0;
        });
        (await factory.ReloadAsync<Transaction>(transaction.Id))!.Value.ShouldBe(100);
        (await client.FinancialResultAsync(s.Manager.Token, Sep)).Entries.Single(e => e.TransactionId == transaction.Id).FinancialClass.ShouldBe(targetClass);
    }

    [Fact]
    public async Task AllocationPublication_MustFollowItsReferencedDrawRevision_InHttpAndSql()
    {
        var day = Sep.AddDays(9);
        using var clock = factory.FreezeBranchLocalTime(day.AddHours(10));
        var s = await FinancialDayScenario.CreateAsync(factory, client, "DrawChronology", businessDate: day);
        var session = await s.StartAsync();
        await s.SaveOkAsync(session, 1015, fields: FinancialDayScenario.Fields(special: 15));
        await client.ConferAsync(session, s.MemberA.Token); await client.SubmitAsync(session, s.MemberA.Token);
        var effect = await factory.ReadDb(db => db.SessionFieldEffects.AsNoTracking().SingleAsync(e => e.SessionId == session && e.Field == SessionCloseField.SpecialFee));
        clock.AdvanceTo(day.AddDays(1).AddHours(10));
        var drawRequest = new RequestSpecialDrawJson { Name = "Synthetic later catalog", Modality = "Explicit", Contest = "123", PeriodFrom = Sep, PeriodTo = new DateTime(2026, 9, 30), Prize = 1000, PostedOn = day.AddDays(1), Reason = "Explicit catalog", Evidence = "Dated source" };
        var created = await client.PostAuthAsync("/people/special-draw", drawRequest, s.Manager.Token, Guid.NewGuid().ToString("N"));
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var draw = await created.ReadContentAsync<ResponseSpecialDrawJson>();
        var request = new RequestSpecialDrawAllocationJson { DrawId = draw.Id, SessionId = session, SessionFieldEffectId = effect.Id, SourceRevision = effect.Revision, SourceDate = day, PostedOn = day, Amount = 5, Reason = "Explicit allocation", Evidence = "Dated allocation" };
        (await client.PostAuthAsync("/people/special-draw-allocation", request, s.Manager.Token, Guid.NewGuid().ToString("N"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var failure = await Should.ThrowAsync<DbUpdateException>(() => factory.ReadDb(async db =>
        {
            db.SpecialDrawAllocations.Add(new SpecialDrawAllocation
            {
                BranchId = s.Branch.Id,
                ActorUserId = s.Manager.User.Id,
                DrawId = draw.Id,
                SessionId = session,
                SessionFieldEffectId = effect.Id,
                SourceRevision = effect.Revision,
                SourceValue = effect.Value,
                SourceDate = day,
                PostedOn = day,
                Amount = 5,
                SignedContribution = 5,
                Reason = "SQL chronology proof",
                Evidence = "Independent guard assertion"
            });
            await db.SaveChangesAsync(); return 0;
        }));
        failure.InnerException.ShouldBeOfType<PostgresException>().ConstraintName.ShouldBe("CK_People_Source");
        async Task<ResponseEmployeeStatementJson> Read(string extra = "") => await (await client.GetAuthAsync($"/report/employee-statement?operatorId={s.MemberA.Operator!.Id}&year=2026&month=9" + extra, s.Manager.Token)).ReadContentAsync<ResponseEmployeeStatementJson>();
        (await Read("&asOfDate=2026-09-10")).SpecialDraws.AllocatedFeeTotal.ShouldBe(0);
        request.PostedOn = day.AddDays(1);
        var accepted = await client.PostAuthAsync("/people/special-draw-allocation", request, s.Manager.Token, Guid.NewGuid().ToString("N"));
        accepted.StatusCode.ShouldBe(HttpStatusCode.Created, await accepted.Content.ReadAsStringAsync());
        (await Read()).SpecialDraws.AllocatedFeeTotal.ShouldBe(5);
        (await Read("&asOfDate=2026-09-10")).SpecialDraws.AllocatedFeeTotal.ShouldBe(0);
        clock.AdvanceTo(day.AddDays(2).AddHours(10));
        drawRequest.PreviousId = draw.Id; drawRequest.PostedOn = day.AddDays(2); drawRequest.Name = "Later explicit revision";
        var revision = await client.PostAuthAsync($"/people/special-draw/{draw.Id}/revision", drawRequest, s.Manager.Token, Guid.NewGuid().ToString("N"), draw.Version);
        revision.StatusCode.ShouldBe(HttpStatusCode.Created, await revision.Content.ReadAsStringAsync());
        request.DrawId = (await revision.ReadContentAsync<ResponseSpecialDrawJson>()).Id;
        (await client.PostAuthAsync("/people/special-draw-allocation", request, s.Manager.Token, Guid.NewGuid().ToString("N"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private Task<int> LockSeptember(Guid branch) => factory.ReadDb(async db => { var setting = await db.Settings.SingleAsync(r => r.BranchId == branch); setting.LockDate = new DateTime(2026, 9, 30); await db.SaveChangesAsync(); return 0; });
}
