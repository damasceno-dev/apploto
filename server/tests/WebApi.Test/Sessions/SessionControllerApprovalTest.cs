using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

/// <summary>
/// M9.6 Phase 5 (5.1–5.4) through HTTP and PostgreSQL: separation of duties, Aprovar como único gestor over a coordinated,
/// current membership count, and the all-or-nothing Aprovar dia do caixa with exact versions and idempotent replay.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class SessionControllerApprovalTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task IndependentManager_ShouldApprove_AndApprovalShouldNotMoveCustody()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveIndependent");
        var session = await MemberSubmittedSessionAsync(scenario, 1_000m);

        var read = (await _client.TryGetSessionAsync(session.Id, scenario.Manager.Token))!;
        read.AllowedActions!.CanApprove.ShouldBeTrue();
        read.AllowedActions.ApprovalBlockedReason.ShouldBeNull();

        var approve = await _client.ApproveSessionAsync(session.Id, scenario.Manager.Token);
        approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        var approved = await approve.ReadContentAsync<ResponseDailyCloseJson>();
        approved.Status.ShouldBe(DailyCloseStatus.Approved);
        approved.ApprovedByUserId.ShouldBe(scenario.Manager.User.Id);
        approved.Custody.CustodyState.ShouldBe(SessionCustodyState.Active);
        var events = await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, session.Id, SessionEventKind.SessionApproved);
        events.ShouldHaveSingleItem().ActorUserId.ShouldBe(scenario.Manager.User.Id);
    }

    [Fact]
    public async Task ManagerWhoRecordedOrSubmitted_ShouldNotApprove_Ordinarily()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveDuties");
        var manager = scenario.Manager.Token;

        // The manager records and submits the member's count: recorder (and submitter).
        var recorded = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await Count(recorded.Session.Id, manager, 1_000m);
        await Submitted(await _client.SubmitSessionAsync(recorded.Session.Id, manager));
        await ShouldRefuse(scenario, recorded.Session.Id, HttpStatusCode.Forbidden, ResourcesErrorMessages.SESSION_APPROVER_IS_RECORDER);

        var blocked = (await _client.TryGetSessionAsync(recorded.Session.Id, manager))!.AllowedActions!;
        blocked.CanApprove.ShouldBeFalse();
        blocked.CanApproveAsSingleManager.ShouldBeTrue();
        blocked.ApprovalBlockedReason.ShouldBe(ResourcesErrorMessages.SESSION_APPROVER_IS_RECORDER);
    }

    [Fact]
    public async Task ManagerWhoOnlySubmitted_ShouldNotApprove_Ordinarily()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveSubmitter");
        var session = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await Count(session.Session.Id, scenario.MemberA.Token, 1_000m);
        await _client.ConferAsync(session.Session.Id, scenario.MemberA.Token);
        await Submitted(await _client.SubmitSessionAsync(session.Session.Id, scenario.Manager.Token));

        await ShouldRefuse(scenario, session.Session.Id, HttpStatusCode.Forbidden, ResourcesErrorMessages.SESSION_APPROVER_IS_SUBMITTER);
    }

    [Fact]
    public async Task ManagerWhoIsTheCustodian_ShouldNotApprove_EvenWhenAnotherManagerCountedAndSubmitted()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveCustodian");
        var managerOperator = await factory.SeedOperatorAsync(scenario.Branch.Id, "Gerente operador", scenario.Manager.User.Id);
        await factory.SeedOperatorAccountAsync(managerOperator.Id, scenario.Cashbox.Id);
        var other = await SecondManagerAsync(scenario);
        var session = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.Manager.Token);
        session.Session.Custody.CustodianOperatorId.ShouldBe(managerOperator.Id);

        await Count(session.Session.Id, other.Token, 1_000m);
        await Submitted(await _client.SubmitSessionAsync(session.Session.Id, other.Token));

        await ShouldRefuse(scenario, session.Session.Id, HttpStatusCode.Forbidden, ResourcesErrorMessages.SESSION_APPROVER_IS_CUSTODIAN);
        // With two active managers the exception is not available either.
        var single = await _client.ApproveSessionAsync(session.Session.Id, scenario.Manager.Token, SessionApprovalMode.SingleActiveManager, "Sozinho");
        single.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await single.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.SESSION_SINGLE_MANAGER_APPROVAL_NOT_AVAILABLE]);
    }

    [Fact]
    public async Task SingleActiveManager_ShouldApproveOwnWork_OnlyWithAReason_AndAudit()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveSingle");
        var manager = scenario.Manager.Token;
        var session = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await Count(session.Session.Id, manager, 1_000m);
        await Submitted(await _client.SubmitSessionAsync(session.Session.Id, manager));

        var withoutReason = await _client.ApproveSessionAsync(session.Session.Id, manager, SessionApprovalMode.SingleActiveManager, "  ");
        withoutReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await withoutReason.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldBe([ResourcesErrorMessages.SESSION_SINGLE_MANAGER_APPROVAL_REQUIRES_REASON]);

        // An inactive manager membership and a manager whose user is inactive are not counted.
        var inactiveMembership = await SecondManagerAsync(scenario);
        await factory.ExecuteSqlAsync($"UPDATE \"BranchUsers\" SET \"Active\" = false WHERE \"Id\" = '{inactiveMembership.Membership.Id}'");
        var inactiveUser = await SecondManagerAsync(scenario);
        await factory.ExecuteSqlAsync($"UPDATE \"Users\" SET \"Active\" = false WHERE \"Id\" = '{inactiveUser.User.Id}'");

        var approve = await _client.ApproveSessionAsync(session.Session.Id, manager, SessionApprovalMode.SingleActiveManager, "Única gestora ativa da filial");
        approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        var approved = (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, session.Session.Id, SessionEventKind.SessionApproved)).ShouldHaveSingleItem();
        approved.Reason.ShouldBe("Única gestora ativa da filial");
        using var details = System.Text.Json.JsonDocument.Parse(approved.Details);
        details.RootElement.GetProperty("mode").GetInt32().ShouldBe((int)SessionApprovalMode.SingleActiveManager);
        details.RootElement.GetProperty("activeManagerCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task SingleManagerApproval_ShouldWaitForAnUncommittedPromotion_AndRefuseOnceItCommits()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveMembershipRace");
        var manager = scenario.Manager.Token;
        var session = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await Count(session.Session.Id, manager, 1_000m);
        await Submitted(await _client.SubmitSessionAsync(session.Session.Id, manager));
        var version = (await _client.TryGetSessionAsync(session.Session.Id, manager))!.Version;

        // A competing promotion of Member B holds its membership row while approval tries to count managers.
        await using (var promotion = await OpenAsync())
        {
            await using var transaction = await promotion.BeginTransactionAsync();
            await Execute(promotion, transaction, $"UPDATE \"BranchUsers\" SET \"Role\" = {(int)Role.Manager} WHERE \"Id\" = '{scenario.MemberB.Membership.Id}'");

            var blocked = await _client.ApproveSessionAsync(
                session.Session.Id, manager, SessionApprovalMode.SingleActiveManager, "Sozinha", expectedVersion: version);
            blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict, await blocked.Content.ReadAsStringAsync());
            (await blocked.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
                .ShouldBe([ResourcesErrorMessages.DAILYCLOSE_LEDGER_COORDINATION_BUSY]);
            (await factory.ReloadAsync<DailyClose>(session.Session.Id))!.Status.ShouldBe(DailyCloseStatus.Submitted);

            await transaction.CommitAsync();
        }

        var afterPromotion = await _client.ApproveSessionAsync(
            session.Session.Id, manager, SessionApprovalMode.SingleActiveManager, "Sozinha", expectedVersion: version);
        afterPromotion.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await afterPromotion.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldBe([ResourcesErrorMessages.SESSION_SINGLE_MANAGER_APPROVAL_NOT_AVAILABLE]);
        (await factory.ReloadAsync<DailyClose>(session.Session.Id))!.Status.ShouldBe(DailyCloseStatus.Submitted);
    }

    [Fact]
    public async Task SingleManagerApproval_ShouldHoldTheMembershipUntilItCommits()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveMembershipHeld");
        var manager = scenario.Manager.Token;
        var session = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await Count(session.Session.Id, manager, 1_000m);
        await Submitted(await _client.SubmitSessionAsync(session.Session.Id, manager));
        // Read the version first: the session read's allowed actions also count managers, without any lock.
        var version = (await _client.TryGetSessionAsync(session.Session.Id, manager))!.Version;
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();
        Task<int>? promotionTask = null;
        var promotionWaited = false;

        HttpResponseMessage approve;
        using (interceptor.Arm(
                   command => command.CommandText.Contains("FROM \"BranchUsers\"", StringComparison.Ordinal)
                              && command.CommandText.Contains("count(*)", StringComparison.OrdinalIgnoreCase),
                   async ct =>
                   {
                       // The approval counted one manager with every membership row locked; a promotion must now wait for its commit.
                       promotionTask = PromoteAsync(scenario.MemberB.Membership.Id);
                       promotionWaited = await WaitUntilBlockedAsync("UPDATE \"BranchUsers\"", ct);
                   }))
        {
            approve = await _client.ApproveSessionAsync(
                session.Session.Id, manager, SessionApprovalMode.SingleActiveManager, "Sozinha", expectedVersion: version);
        }

        approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        promotionWaited.ShouldBeTrue();
        (await promotionTask!.WaitAsync(TimeSpan.FromSeconds(15))).ShouldBe(1);
        (await factory.ReloadAsync<BranchUser>(scenario.MemberB.Membership.Id))!.Role.ShouldBe(Role.Manager);
        (await factory.ReloadAsync<DailyClose>(session.Session.Id))!.Status.ShouldBe(DailyCloseStatus.Approved);
    }

    [Fact]
    public async Task SingleManagerApproval_ShouldHoldTheReactivationOfAnInactiveManagerUntilItCommits()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveReactivationHeld");
        var formerManager = await SecondManagerAsync(scenario);
        await factory.ExecuteSqlAsync($"UPDATE \"BranchUsers\" SET \"Active\" = false WHERE \"Id\" = '{formerManager.Membership.Id}'");
        var manager = scenario.Manager.Token;
        var session = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await Count(session.Session.Id, manager, 1_000m);
        await Submitted(await _client.SubmitSessionAsync(session.Session.Id, manager));
        var version = (await _client.TryGetSessionAsync(session.Session.Id, manager))!.Version;
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();
        Task<int>? reactivationTask = null;
        var reactivationWaited = false;

        HttpResponseMessage approve;
        using (interceptor.Arm(IsManagerCount, async ct =>
               {
                   // Approval counted one manager; reactivating the inactive one must wait for the approval's commit.
                   reactivationTask = ExecuteAsync(
                       $"UPDATE \"BranchUsers\" SET \"Active\" = true WHERE \"Id\" = '{formerManager.Membership.Id}'");
                   reactivationWaited = await WaitUntilBlockedAsync("UPDATE \"BranchUsers\"", ct);
               }))
        {
            approve = await _client.ApproveSessionAsync(
                session.Session.Id, manager, SessionApprovalMode.SingleActiveManager, "Sozinha", expectedVersion: version);
        }

        approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        reactivationWaited.ShouldBeTrue();
        (await reactivationTask!.WaitAsync(TimeSpan.FromSeconds(15))).ShouldBe(1);
        (await factory.ReloadAsync<BranchUser>(formerManager.Membership.Id))!.Active.ShouldBeTrue();
        (await factory.ReloadAsync<DailyClose>(session.Session.Id))!.Status.ShouldBe(DailyCloseStatus.Approved);
    }

    [Fact]
    public async Task SingleManagerApproval_ShouldHoldAMembershipCommandUntilItCommits()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveRoleCommandHeld");
        var manager = scenario.Manager.Token;
        var session = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await Count(session.Session.Id, manager, 1_000m);
        await Submitted(await _client.SubmitSessionAsync(session.Session.Id, manager));
        var version = (await _client.TryGetSessionAsync(session.Session.Id, manager))!.Version;
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();
        Task<HttpResponseMessage>? promotionTask = null;
        var promotionWaited = false;

        HttpResponseMessage approve;
        using (interceptor.Arm(IsManagerCount, async ct =>
               {
                   // The role command takes the branch security lock exclusively, so it waits for the approval.
                   promotionTask = _client.PutAuthAsync(
                       $"/branch/users/{scenario.MemberB.Membership.Id}/role",
                       new RequestUpdateBranchUserRoleJson { Role = Role.Manager },
                       manager);
                   promotionWaited = await WaitUntilBlockedAsync("SELECT pg_advisory_xact_lock(", ct);
               }))
        {
            approve = await _client.ApproveSessionAsync(
                session.Session.Id, manager, SessionApprovalMode.SingleActiveManager, "Sozinha", expectedVersion: version);
        }

        approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
        promotionWaited.ShouldBeTrue();
        var promotion = await promotionTask!.WaitAsync(TimeSpan.FromSeconds(15));
        promotion.StatusCode.ShouldBe(HttpStatusCode.OK, await promotion.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<BranchUser>(scenario.MemberB.Membership.Id))!.Role.ShouldBe(Role.Manager);
        (await factory.ReloadAsync<DailyClose>(session.Session.Id))!.Status.ShouldBe(DailyCloseStatus.Approved);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Approval_ShouldRefuse_WhenTheApproverIsDemotedBeforeTheCoordination(bool approveDay)
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveDemoted");
        var admin = await SecondMemberAsync(scenario, Role.Admin);
        var (first, second) = await HandedOverDayAsync(scenario);
        var interceptor = factory.Services.GetRequiredService<ArmableCommandInterceptor>();
        HttpResponseMessage? demotion = null;

        HttpResponseMessage approve;
        using (interceptor.Arm(
                   command => command.CommandText.Contains("FROM \"DailyCloses\"", StringComparison.Ordinal),
                   async _ =>
                   {
                       // Authenticated as Manager; demoted and committed before the approval reaches its locks.
                       demotion = await _client.PutAuthAsync(
                           $"/branch/users/{scenario.Manager.Membership.Id}/role",
                           new RequestUpdateBranchUserRoleJson { Role = Role.Member },
                           admin.Token);
                   }))
        {
            approve = approveDay
                ? await PostApproveDay(scenario, first, second)
                : await _client.ApproveSessionAsync(first.Id, scenario.Manager.Token, expectedVersion: first.Version);
        }

        demotion!.StatusCode.ShouldBe(HttpStatusCode.OK, await demotion.Content.ReadAsStringAsync());
        approve.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await approve.Content.ReadAsStringAsync());
        (await approve.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION]);
        await ShouldBeUntouched(scenario, first, second);
    }

    [Fact]
    public async Task ApproveDay_ShouldApproveEverySessionOfTheCashboxDay_AndKeepCustody()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveDayOk");
        var (first, second) = await HandedOverDayAsync(scenario);

        var response = await PostApproveDay(scenario, first, second);

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var approved = await response.ReadContentAsync<ResponseApproveSessionBatchJson>();
        approved.Sessions.Select(session => session.Id).ShouldBe([first.Id, second.Id]);
        approved.Sessions.ShouldAllBe(session => session.Status == DailyCloseStatus.Approved);
        (await factory.ReloadAsync<DailyClose>(first.Id))!.CustodyState.ShouldBe(SessionCustodyState.HandedOver);
        (await factory.ReloadAsync<DailyClose>(second.Id))!.CustodyState.ShouldBe(SessionCustodyState.Finished);
        var firstEvent = (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, first.Id, SessionEventKind.SessionApproved)).ShouldHaveSingleItem();
        var secondEvent = (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, second.Id, SessionEventKind.SessionApproved)).ShouldHaveSingleItem();
        firstEvent.Details.ShouldContain("batchId");
        secondEvent.Details.ShouldContain(first.Id.ToString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task ApproveDay_ShouldApproveNothing_WhenAnySessionIsIneligible(int ineligiblePosition)
    {
        var scenario = await SessionScenario.CreateAsync(factory, $"ApproveDayIneligible{ineligiblePosition}");
        var (first, second) = await HandedOverDayAsync(scenario, managerSubmitsSecond: ineligiblePosition == 1, managerSubmitsFirst: ineligiblePosition == 0);
        var ineligible = ineligiblePosition == 0 ? first : second;

        var response = await PostApproveDay(scenario, first, second);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([
            string.Format(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_ITEM_FAILED, ineligible.Id, ResourcesErrorMessages.SESSION_APPROVER_IS_SUBMITTER)
        ]);
        await ShouldBeUntouched(scenario, first, second);
    }

    [Fact]
    public async Task ApproveDay_ShouldApproveNothing_WhenAVersionIsStaleOrASessionIsNotSubmittedOrLocked()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveDayStale");
        var (first, second) = await HandedOverDayAsync(scenario);

        var stale = await _client.PostAuthAsync(
            "/session/approve-day",
            new RequestApproveSessionBatchJson
            {
                Sessions =
                [
                    new RequestApproveSessionBatchItemJson { SessionId = first.Id, Version = first.Version },
                    new RequestApproveSessionBatchItemJson { SessionId = second.Id, Version = second.Version + 7 }
                ]
            },
            scenario.Manager.Token);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stale.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([
            string.Format(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_ITEM_FAILED, second.Id, ResourcesErrorMessages.SESSION_STALE_WRITE)
        ]);
        await ShouldBeUntouched(scenario, first, second);

        await factory.SeedSettingAsync(scenario.Branch.Id, lockDate: SessionFlow.LocalToday());
        var locked = await PostApproveDay(scenario, first, second);
        locked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await locked.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.Count.ShouldBe(2);
        await ShouldBeUntouched(scenario, first, second);
    }

    [Fact]
    public async Task ApproveDay_ShouldRefuseADraftSession_AndSessionsOfAnotherCashbox()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveDayScope");
        var submitted = await MemberSubmittedSessionAsync(scenario, 1_000m);
        var terminalId = (await factory.ReloadAsync<Account>(scenario.Cashbox.Id))!.TerminalId!.Value;
        var otherCashbox = await scenario.SecondCashboxAsync(factory, terminalId);
        var draft = (await _client.StartSessionAsync(otherCashbox.Id, scenario.MemberB.Token)).Session;

        var mixed = await PostApproveDay(scenario, submitted, draft);
        mixed.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await mixed.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.SESSION_BATCH_APPROVAL_SCOPE_MIXED]);

        var notSubmitted = await PostApproveDay(scenario, draft);
        notSubmitted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await notSubmitted.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([
            string.Format(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_ITEM_FAILED, draft.Id, ResourcesErrorMessages.DAILYCLOSE_NOT_APPROVABLE)
        ]);
        (await factory.ReloadAsync<DailyClose>(submitted.Id))!.Status.ShouldBe(DailyCloseStatus.Submitted);
    }

    [Fact]
    public async Task ApproveDay_ShouldReplayTheSameOutcome_AndRefuseAChangedPayloadOrLostAuthority()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveDayReplay");
        var (first, second) = await HandedOverDayAsync(scenario);
        var request = Batch(first, second);

        var original = await _client.PostAuthAsync("/session/approve-day", request, scenario.Manager.Token, "approve-day-1");
        original.StatusCode.ShouldBe(HttpStatusCode.OK, await original.Content.ReadAsStringAsync());
        var originalBody = await original.ReadContentAsync<ResponseApproveSessionBatchJson>();

        var replay = await _client.PostAuthAsync("/session/approve-day", request, scenario.Manager.Token, "approve-day-1");
        replay.StatusCode.ShouldBe(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
        (await replay.ReadContentAsync<ResponseApproveSessionBatchJson>()).Sessions.Select(session => session.Version)
            .ShouldBe(originalBody.Sessions.Select(session => session.Version));
        (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, first.Id, SessionEventKind.SessionApproved)).ShouldHaveSingleItem();

        var changed = await _client.PostAuthAsync("/session/approve-day", Batch(first), scenario.Manager.Token, "approve-day-1");
        changed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await changed.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.IDEMPOTENCY_KEY_PAYLOAD_CONFLICT]);

        await factory.ExecuteSqlAsync($"UPDATE \"BranchUsers\" SET \"Role\" = {(int)Role.Member} WHERE \"Id\" = '{scenario.Manager.Membership.Id}'");
        var lostAuthority = await _client.PostAuthAsync("/session/approve-day", request, scenario.Manager.Token, "approve-day-1");
        lostAuthority.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await lostAuthority.Content.ReadAsStringAsync()).ShouldNotContain(first.Id.ToString());
    }

    [Fact]
    public async Task ApproveSingle_ShouldReplayTheSameOutcomeForTheSameKey()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "ApproveReplay");
        var session = await MemberSubmittedSessionAsync(scenario, 1_000m);
        var version = (await _client.TryGetSessionAsync(session.Id, scenario.Manager.Token))!.Version;

        var first = await _client.ApproveSessionAsync(session.Id, scenario.Manager.Token, idempotencyKey: "approve-1", expectedVersion: version);
        var second = await _client.ApproveSessionAsync(session.Id, scenario.Manager.Token, idempotencyKey: "approve-1", expectedVersion: version);
        var otherKey = await _client.ApproveSessionAsync(session.Id, scenario.Manager.Token, idempotencyKey: "approve-2", expectedVersion: version);

        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.ReadContentAsync<ResponseDailyCloseJson>()).Version.ShouldBe((await first.ReadContentAsync<ResponseDailyCloseJson>()).Version);
        otherKey.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, session.Id, SessionEventKind.SessionApproved)).ShouldHaveSingleItem();
    }

    private async Task<ResponseDailyCloseJson> MemberSubmittedSessionAsync(SessionScenario scenario, decimal cash)
    {
        var session = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await Count(session.Session.Id, scenario.MemberA.Token, cash);
        await Submitted(await _client.SubmitSessionAsync(session.Session.Id, scenario.MemberA.Token));
        return (await _client.TryGetSessionAsync(session.Session.Id, scenario.MemberA.Token))!;
    }

    /// <summary>Member A hands the shared cashbox to Member B; both sessions of the day end Submitted.</summary>
    private async Task<(ResponseDailyCloseJson First, ResponseDailyCloseJson Second)> HandedOverDayAsync(
        SessionScenario scenario,
        bool managerSubmitsFirst = false,
        bool managerSubmitsSecond = false)
    {
        var first = await _client.StartSessionAsync(scenario.Cashbox.Id, scenario.MemberA.Token);
        await Count(first.Session.Id, scenario.MemberA.Token, 0m);
        await _client.ConferAsync(first.Session.Id, scenario.MemberA.Token);
        await Submitted(await _client.SubmitSessionAsync(first.Session.Id, managerSubmitsFirst ? scenario.Manager.Token : scenario.MemberA.Token));
        var offer = await _client.OfferAsync(first.Session.Id, scenario.MemberB.Operator!.Id, scenario.MemberA.Token);
        offer.OfferedAttemptId.ShouldNotBeNull();
        var accept = await _client.PostAuthAsync(
            $"/session/handover/{offer.Id}/accept",
            new RequestAcceptSessionHandoverJson { Items = [new RequestSessionOpeningItemJson { ProductId = scenario.Cash.Id, Value = 0m }], ExplicitZero = true },
            scenario.MemberB.Token,
            expectedVersion: offer.Version);
        accept.StatusCode.ShouldBe(HttpStatusCode.OK, await accept.Content.ReadAsStringAsync());
        var second = (await accept.ReadContentAsync<ResponseAcceptSessionHandoverJson>()).Incoming.Session;
        await Count(second.Id, scenario.MemberB.Token, 0m);
        await _client.ConferAsync(second.Id, scenario.MemberB.Token);
        await Submitted(await _client.SubmitSessionAsync(second.Id, managerSubmitsSecond ? scenario.Manager.Token : scenario.MemberB.Token));
        await _client.FinishAsync(second.Id, scenario.MemberB.Token);
        return ((await _client.TryGetSessionAsync(first.Session.Id, scenario.Manager.Token))!,
            (await _client.TryGetSessionAsync(second.Id, scenario.Manager.Token))!);
    }

    private async Task Count(Guid sessionId, string token, decimal cash)
    {
        var scenarioCash = await CashProductOf(sessionId);
        var response = await SessionCountScenario.SaveCountAsync(_client, sessionId, token, new Dictionary<Guid, decimal> { [scenarioCash] = cash });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<Guid> CashProductOf(Guid sessionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var branchId = await dbContext.DailyCloses.Where(close => close.Id == sessionId).Select(close => close.BranchId).SingleAsync();
        return await dbContext.Products.Where(product => product.BranchId == branchId && product.Name == "Dinheiro").Select(product => product.Id).SingleAsync();
    }

    private static async Task Submitted(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task ShouldRefuse(SessionScenario scenario, Guid sessionId, HttpStatusCode status, string message)
    {
        var response = await _client.ApproveSessionAsync(sessionId, scenario.Manager.Token);
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([message]);
        (await factory.ReloadAsync<DailyClose>(sessionId))!.Status.ShouldBe(DailyCloseStatus.Submitted);
        (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, sessionId, SessionEventKind.SessionApproved)).ShouldBeEmpty();
    }

    private async Task ShouldBeUntouched(SessionScenario scenario, params ResponseDailyCloseJson[] sessions)
    {
        foreach (var session in sessions)
        {
            var reloaded = (await factory.ReloadAsync<DailyClose>(session.Id))!;
            reloaded.Status.ShouldBe(DailyCloseStatus.Submitted);
            reloaded.ApprovedByUserId.ShouldBeNull();
            (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, session.Id, SessionEventKind.SessionApproved)).ShouldBeEmpty();
        }
    }

    private Task<HttpResponseMessage> PostApproveDay(SessionScenario scenario, params ResponseDailyCloseJson[] sessions)
    {
        return _client.PostAuthAsync("/session/approve-day", Batch(sessions), scenario.Manager.Token);
    }

    private static RequestApproveSessionBatchJson Batch(params ResponseDailyCloseJson[] sessions)
    {
        return new RequestApproveSessionBatchJson
        {
            Sessions = [.. sessions.Select(session => new RequestApproveSessionBatchItemJson { SessionId = session.Id, Version = session.Version })]
        };
    }

    private Task<SessionActor> SecondManagerAsync(SessionScenario scenario)
    {
        return SecondMemberAsync(scenario, Role.Manager);
    }

    private async Task<SessionActor> SecondMemberAsync(SessionScenario scenario, Role role)
    {
        var user = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(user.Id, scenario.Branch.Id, role);
        return new SessionActor(user, membership, null, factory.IssueBranchToken(membership));
    }

    private static bool IsManagerCount(System.Data.Common.DbCommand command)
    {
        return command.CommandText.Contains("FROM \"BranchUsers\"", StringComparison.Ordinal)
               && command.CommandText.Contains("count(*)", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<int> ExecuteAsync(string sql)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var connection = new NpgsqlConnection(scope.ServiceProvider.GetRequiredService<ServerDbContext>().Database.GetConnectionString());
        await connection.OpenAsync();
        return connection;
    }

    private static async Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<int> PromoteAsync(Guid membershipId)
    {
        await using var connection = await OpenAsync();
        await using var command = new NpgsqlCommand(
            $"UPDATE \"BranchUsers\" SET \"Role\" = {(int)Role.Manager} WHERE \"Id\" = '{membershipId}'",
            connection);
        return await command.ExecuteNonQueryAsync();
    }

    private async Task<bool> WaitUntilBlockedAsync(string queryPrefix, CancellationToken ct)
    {
        await using var connection = await OpenAsync();
        for (var attempt = 0; attempt < 200; attempt++)
        {
            await using var command = new NpgsqlCommand(
                "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE @prefix",
                connection);
            command.Parameters.AddWithValue("prefix", queryPrefix + "%");
            if ((long)(await command.ExecuteScalarAsync(ct))! > 0)
                return true;
            await Task.Delay(25, ct);
        }

        return false;
    }
}
