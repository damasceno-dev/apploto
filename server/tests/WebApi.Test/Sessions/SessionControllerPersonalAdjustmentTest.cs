using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

[Collection(ServerApiCollection.Name)]
public class SessionControllerPersonalAdjustmentTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task DatabaseAttributionGuard_ConcurrentSuccessorsCannotForkTheSameHead()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 9, 10, 8, 0, 0));
        var (s, source, _) = await StandaloneSource(clock, lockSource: false);
        OpeningDiscrepancyAttributionEvent Decision(Guid? previous) => new()
        {
            DiscrepancyId = source.Id, DiscrepancyRevision = 1, SupersedesEventId = previous,
            Target = OpeningDiscrepancyAttribution.Incoming, ActorUserId = s.Manager.User.Id, Reason = "Concurrent database guard"
        };
        var root = Decision(null);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            db.Add(root); await db.SaveChangesAsync();
        }
        async Task<bool> Append()
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            db.Add(Decision(root.Id));
            try { await db.SaveChangesAsync(); return true; }
            catch (DbUpdateException ex)
            {
                ex.InnerException.ShouldBeOfType<Npgsql.PostgresException>().ConstraintName.ShouldBe("CK_OpeningDiscrepancyAttributionEvents_Chain");
                return false;
            }
        }
        var outcomes = await Task.WhenAll(Append(), Append());
        outcomes.Count(success => success).ShouldBe(1);
        using var verify = factory.Services.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<ServerDbContext>();
        (await context.OpeningDiscrepancyAttributionEvents.CountAsync(e => e.DiscrepancyId == source.Id)).ShouldBe(2);
        (await client.GetAuthAsync($"/session/opening-discrepancy/{source.Id}/responsibility", s.Manager.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LegacyFork_IsAStableConflictOnAllRoutes_AndDatabaseRefusesNewForks()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 9, 10, 8, 0, 0));
        var (s, source, setting) = await StandaloneSource(clock, lockSource: false);
        OpeningDiscrepancyAttributionEvent Decision() => new()
        {
            DiscrepancyId = source.Id, DiscrepancyRevision = 1,
            Target = OpeningDiscrepancyAttribution.Incoming, ActorUserId = s.Manager.User.Id,
            Reason = "Synthetic prior-schema decision"
        };
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            db.Add(Decision());
            await db.SaveChangesAsync();
            db.Add(Decision());
            var refused = await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync());
            refused.InnerException.ShouldBeOfType<Npgsql.PostgresException>().ConstraintName.ShouldBe("CK_OpeningDiscrepancyAttributionEvents_Chain");
            db.ChangeTracker.Clear();
            // Reproduce the pre-upgrade bug without rewriting any history. The table lock is held
            // only in this owned fixture transaction, and the new guard is restored before commit.
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"OpeningDiscrepancyAttributionEvents\" DISABLE TRIGGER \"TR_OpeningDiscrepancyAttributionEvents_Chain\"");
            db.Add(Decision());
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE \"OpeningDiscrepancyAttributionEvents\" ENABLE TRIGGER \"TR_OpeningDiscrepancyAttributionEvents_Chain\"");
            await tx.CommitAsync();
        }
        async Task Ambiguous(HttpResponseMessage response)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
            (await response.Content.ReadAsStringAsync()).ShouldContain(ResourcesErrorMessages.OPENING_DISCREPANCY_ATTRIBUTION_AMBIGUOUS);
        }
        await Ambiguous(await client.GetAuthAsync($"/session/opening-discrepancy/{source.Id}/responsibility", s.Manager.Token));
        await Ambiguous(await client.PostAuthAsync($"/session/opening-discrepancy/{source.Id}/attribution",
            new RequestAttributeOpeningDiscrepancyJson { DiscrepancyRevision = 1, Target = OpeningDiscrepancyAttribution.Neither, Reason = "No guessing" },
            s.Manager.Token, expectedVersion: source.Version));
        var locked = await client.PostAuthAsync("/setting/lock-month", new RequestLockSettingMonthJson { Year = 2026, Month = 9 },
            s.Manager.Token, expectedVersion: setting.Version);
        locked.StatusCode.ShouldBe(HttpStatusCode.OK, await locked.Content.ReadAsStringAsync());
        await Ambiguous(await client.PostAuthAsync("/session/opening-discrepancy-adjustment", new RequestOpeningDiscrepancyAdjustmentJson
        {
            DiscrepancyId = source.Id, ComparisonRevision = 1, ExpectedVersion = source.Version,
            Target = OpeningDiscrepancyAttribution.Incoming, PostedOn = new DateTime(2026, 10, 2), Reason = "No guessing"
        }, s.Manager.Token));
        using var verify = factory.Services.CreateScope();
        var context = verify.ServiceProvider.GetRequiredService<ServerDbContext>();
        (await context.OpeningDiscrepancyAttributionEvents.CountAsync(e => e.DiscrepancyId == source.Id)).ShouldBe(2);
        (await context.OpeningDiscrepancyAdjustments.CountAsync(e => e.DiscrepancyId == source.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task AdjustmentDatesAndOpenSource_ReturnTheirOwnErrors_WithoutReservingAnOutcome()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 9, 10, 8, 0, 0));
        var (s, source, setting) = await StandaloneSource(clock, lockSource: false);
        RequestOpeningDiscrepancyAdjustmentJson Request(DateTime date) => new()
        {
            DiscrepancyId = source.Id, ExpectedVersion = source.Version, ComparisonRevision = 1,
            Target = OpeningDiscrepancyAttribution.Incoming, PostedOn = date, Reason = "Validate source and date"
        };
        var open = await client.PostAuthAsync("/session/opening-discrepancy-adjustment", Request(new DateTime(2026, 10, 2)), s.Manager.Token);
        open.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await open.Content.ReadAsStringAsync()).ShouldContain(ResourcesErrorMessages.OPENING_DISCREPANCY_ADJUSTMENT_SOURCE_OPEN);
        var locked = await client.PostAuthAsync("/setting/lock-month", new RequestLockSettingMonthJson { Year = 2026, Month = 9 },
            s.Manager.Token, expectedVersion: setting.Version);
        locked.StatusCode.ShouldBe(HttpStatusCode.OK, await locked.Content.ReadAsStringAsync());
        var future = await client.PostAuthAsync("/session/opening-discrepancy-adjustment", Request(new DateTime(2026, 10, 6)), s.Manager.Token);
        future.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await future.Content.ReadAsStringAsync()).ShouldContain(ResourcesErrorMessages.OPENING_DISCREPANCY_ADJUSTMENT_DATE_INVALID);
        var past = await client.PostAuthAsync("/session/opening-discrepancy-adjustment", Request(new DateTime(2026, 9, 30)), s.Manager.Token);
        past.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await past.Content.ReadAsStringAsync()).ShouldContain(ResourcesErrorMessages.OPENING_DISCREPANCY_LOCKED);
    }

    [Fact]
    public async Task AttributionHead_UsesAppendLinksInsteadOfWallClock_AndLateClearOffsetsTheLockedOriginal()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 9, 10, 8, 0, 0));
        var (s, source, setting) = await StandaloneSource(clock, lockSource: false);
        clock.AdvanceTo(new DateTime(2026, 10, 5, 12, 0, 0));
        var first = await client.PostAuthAsync($"/session/opening-discrepancy/{source.Id}/attribution",
            new RequestAttributeOpeningDiscrepancyJson { DiscrepancyRevision = 1, Target = OpeningDiscrepancyAttribution.Neither, Reason = "Primeira decisão" },
            s.Manager.Token, expectedVersion: source.Version);
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var firstVersion = (await first.ReadContentAsync<ResponseOpeningDiscrepancyJson>()).Version;
        // A clock adjustment cannot make the prior decision become current again.
        clock.AdvanceTo(new DateTime(2026, 10, 5, 11, 0, 0));
        var second = await client.PostAuthAsync($"/session/opening-discrepancy/{source.Id}/attribution",
            new RequestAttributeOpeningDiscrepancyJson { DiscrepancyRevision = 1, Target = OpeningDiscrepancyAttribution.Incoming, Reason = "Decisão corrigida" },
            s.Manager.Token, expectedVersion: firstVersion);
        second.StatusCode.ShouldBe(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
        var current = await second.ReadContentAsync<ResponseOpeningDiscrepancyJson>();
        var context = await (await client.GetAuthAsync($"/session/opening-discrepancy/{source.Id}/responsibility", s.Manager.Token))
            .ReadContentAsync<ResponseOpeningResponsibilityJson>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var actualHead = await db.OpeningDiscrepancyAttributionEvents.AsNoTracking()
                .SingleAsync(a => a.DiscrepancyId == source.Id && a.Target == OpeningDiscrepancyAttribution.Incoming);
            context.LatestAttributionId.ShouldBe(actualHead.Id);
            actualHead.SupersedesEventId.ShouldNotBeNull();
        }
        var previousHead = context.LatestAttributionId;
        var sameTarget = await client.PostAuthAsync($"/session/opening-discrepancy/{source.Id}/attribution",
            new RequestAttributeOpeningDiscrepancyJson { DiscrepancyRevision = 1, Target = OpeningDiscrepancyAttribution.Incoming, Reason = "Evidência complementar" },
            s.Manager.Token, expectedVersion: current.Version);
        sameTarget.StatusCode.ShouldBe(HttpStatusCode.OK, await sameTarget.Content.ReadAsStringAsync());
        var revisedDecision = await sameTarget.ReadContentAsync<ResponseOpeningDiscrepancyJson>();
        revisedDecision.Version.ShouldNotBe(current.Version);
        var stale = await client.PostAuthAsync($"/session/opening-discrepancy/{source.Id}/attribution",
            new RequestAttributeOpeningDiscrepancyJson { DiscrepancyRevision = 1, Target = OpeningDiscrepancyAttribution.Neither, Reason = "Cabeça desatualizada" },
            s.Manager.Token, expectedVersion: current.Version);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        current = revisedDecision;
        context = await (await client.GetAuthAsync($"/session/opening-discrepancy/{source.Id}/responsibility", s.Manager.Token))
            .ReadContentAsync<ResponseOpeningResponsibilityJson>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var appended = await db.OpeningDiscrepancyAttributionEvents.AsNoTracking().SingleAsync(a => a.SupersedesEventId == previousHead);
            context.LatestAttributionId.ShouldBe(appended.Id);
            (await db.OpeningDiscrepancyAttributionEvents.CountAsync(a => a.DiscrepancyId == source.Id)).ShouldBe(3);
        }
        var locked = await client.PostAuthAsync("/setting/lock-month", new RequestLockSettingMonthJson { Year = 2026, Month = 9 },
            s.Manager.Token, expectedVersion: setting.Version);
        locked.StatusCode.ShouldBe(HttpStatusCode.OK, await locked.Content.ReadAsStringAsync());
        var afterLockSetting = await locked.ReadContentAsync<ResponseSettingJson>();
        var configured = await client.PutAuthAsync("/setting", new RequestUpdateSettingJson { PersonalShortageThreshold = 5m },
            s.Manager.Token, afterLockSetting.Version);
        configured.StatusCode.ShouldBe(HttpStatusCode.OK, await configured.Content.ReadAsStringAsync());
        var thisMonth = await client.StartSessionAsync(s.Cashbox.Id, s.MemberA.Token);
        await client.CountAsync(thisMonth.Session.Id, s.MemberA.Token, new Dictionary<Guid, decimal> { [s.Cash.Id] = 975m });
        await client.SubmitAsync(thisMonth.Session.Id, s.MemberA.Token);
        clock.AdvanceTo(new DateTime(2026, 10, 5, 11, 15, 0));
        await client.FinishAsync(thisMonth.Session.Id, s.MemberA.Token);
        (await client.ApproveSessionAsync(thisMonth.Session.Id, s.Manager.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var adjusted = await client.PostAuthAsync("/session/opening-discrepancy-adjustment", new RequestOpeningDiscrepancyAdjustmentJson
        {
            DiscrepancyId = source.Id, ExpectedVersion = current.Version, ComparisonRevision = 1,
            PriorAttributionId = context.LatestAttributionId, Target = OpeningDiscrepancyAttribution.Neither,
            PostedOn = new DateTime(2026, 10, 5), Reason = "Retirada de responsabilidade com evidência"
        }, s.Manager.Token);
        adjusted.StatusCode.ShouldBe(HttpStatusCode.OK, await adjusted.Content.ReadAsStringAsync());
        var old = await client.PersonalDifferencesAsync(s.Manager.Token, new DateTime(2026, 9, 10));
        old.People.Single(p => p.OperatorId == s.MemberA.Operator!.Id).Shortage.ShouldBe(20m);
        var posted = await client.PersonalDifferencesAsync(s.Manager.Token, new DateTime(2026, 10, 5));
        var offset = posted.People.Single(p => p.OperatorId == s.MemberA.Operator!.Id);
        (offset.Shortage, offset.Overage, offset.SignedNet).ShouldBe((-15m, 0m, 15m));
        // The historical correction credit is visible but cannot hide this month's actual shortage of 5.
        offset.ThresholdShortageBasis.ShouldBe(5m);
        offset.CurrentThresholdAlert.ShouldBeTrue();
        var statementResponse = await client.GetAuthAsync($"/report/employee-statement?operatorId={s.MemberA.Operator!.Id}&year=2026&month=10", s.Manager.Token);
        statementResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await statementResponse.Content.ReadAsStringAsync());
        var statement = await statementResponse.ReadContentAsync<ResponseEmployeeStatementJson>();
        statement.Differences.People.Single().Shortage.ShouldBe(-15m);
        statement.Differences.People.Single().ThresholdShortageBasis.ShouldBe(5m);
        statement.RecordedPaySignedTotal.ShouldBe(0m); // late responsibility offsets never become pay items
        statement.Commission.ProposedAmount.ShouldBeNull();
        var oldStatement = await (await client.GetAuthAsync($"/report/employee-statement?operatorId={s.MemberA.Operator.Id}&year=2026&month=9&asOfDate=2026-09-30", s.Manager.Token)).ReadContentAsync<ResponseEmployeeStatementJson>();
        oldStatement.Differences.People.Single().Shortage.ShouldBe(20m);

    }

    [Fact]
    public async Task FailedDatabaseFlush_RollsBackAdjustmentAndReplayReservation_AndAllowsExactRetry()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 9, 10, 8, 0, 0));
        var (s, source, _) = await StandaloneSource(clock, lockSource: true);
        var request = new RequestOpeningDiscrepancyAdjustmentJson
        {
            DiscrepancyId = source.Id, ExpectedVersion = source.Version, ComparisonRevision = 1,
            Target = OpeningDiscrepancyAttribution.Incoming, PostedOn = new DateTime(2026, 10, 2), Reason = "Ajuste com evidência"
        };
        var key = Guid.NewGuid().ToString();
        var injected = false;
        using (factory.Services.GetRequiredService<ArmableCommandInterceptor>().Arm(
            command => command.CommandText.Contains("INSERT INTO \"OpeningDiscrepancyAdjustments\"", StringComparison.Ordinal),
            _ => { injected = true; throw new InvalidOperationException("Injected responsibility persistence failure"); }))
        {
            var response = await client.PostAuthAsync("/session/opening-discrepancy-adjustment", request, s.Manager.Token, key);
            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError, await response.Content.ReadAsStringAsync());
        }
        injected.ShouldBeTrue();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            (await db.OpeningDiscrepancyAdjustments.CountAsync(a => a.BranchId == s.Branch.Id)).ShouldBe(0);
            (await db.IdempotencyRequests.CountAsync(i => i.BranchId == s.Branch.Id && i.Key == key)).ShouldBe(0);
            (await db.Transactions.CountAsync(t => t.BranchId == s.Branch.Id)).ShouldBe(0);
        }
        var retry = await client.PostAuthAsync("/session/opening-discrepancy-adjustment", request, s.Manager.Token, key);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        (await retry.ReadContentAsync<ResponseOpeningDiscrepancyAdjustmentJson>()).Sequence.ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AttributionAndMonthLock_UseTheSameBoundary(bool attributionFirst)
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 9, 10, 8, 0, 0));
        var (s, source, setting) = await StandaloneSource(clock, lockSource: false);
        await using var held = await HeldAdvisoryLock.AcquireAsync(factory.DisposableConnectionString,
            server.Infrastructure.Services.MonthLockCoordinationKey.Compute(s.Branch.Id));
        Task<HttpResponseMessage> Attribute() => client.PostAuthAsync($"/session/opening-discrepancy/{source.Id}/attribution",
            new RequestAttributeOpeningDiscrepancyJson { DiscrepancyRevision = 1, Target = OpeningDiscrepancyAttribution.Incoming, Reason = "Atribuição conferida" },
            s.Manager.Token, expectedVersion: source.Version);
        Task<HttpResponseMessage> Lock() => client.PostAuthAsync("/setting/lock-month",
            new RequestLockSettingMonthJson { Year = 2026, Month = 9 }, s.Manager.Token, expectedVersion: setting.Version);
        var first = attributionFirst ? Attribute() : Lock();
        await held.WaitForWaitersAsync(1);
        var second = attributionFirst ? Lock() : Attribute();
        await held.WaitForWaitersAsync(2);
        await held.ReleaseAsync();
        var replies = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(15));
        var attributed = replies[attributionFirst ? 0 : 1];
        var locked = replies[attributionFirst ? 1 : 0];
        locked.StatusCode.ShouldBe(HttpStatusCode.OK, await locked.Content.ReadAsStringAsync());
        attributed.StatusCode.ShouldBe(attributionFirst ? HttpStatusCode.OK : HttpStatusCode.Conflict, await attributed.Content.ReadAsStringAsync());
        var retained = (await factory.ReloadAsync<SessionOpeningDiscrepancy>(source.Id))!;
        retained.Attribution.ShouldBe(attributionFirst ? OpeningDiscrepancyAttribution.Incoming : OpeningDiscrepancyAttribution.Unattributed);
        var report = await (await client.GetAuthAsync("/report/personal-differences?year=2026&month=9", s.Manager.Token)).ReadContentAsync<ResponsePersonalDifferencesJson>();
        report.People.Single(p => p.OperatorId == s.MemberA.Operator!.Id).Shortage.ShouldBe(attributionFirst ? 20m : 0m);
        report.People.ShouldAllBe(p => !p.CurrentThresholdAlert);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LateAdjustmentAndPostedMonthLock_CommitInBoundaryOrder(bool adjustmentFirst)
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 9, 10, 8, 0, 0));
        var (s, source, setting) = await StandaloneSource(clock, lockSource: true);
        clock.AdvanceTo(new DateTime(2026, 11, 5, 10, 0, 0));
        await using var held = await HeldAdvisoryLock.AcquireAsync(factory.DisposableConnectionString,
            server.Infrastructure.Services.MonthLockCoordinationKey.Compute(s.Branch.Id));
        Task<HttpResponseMessage> Adjust() => client.PostAuthAsync("/session/opening-discrepancy-adjustment",
            new RequestOpeningDiscrepancyAdjustmentJson
            {
                DiscrepancyId = source.Id, ExpectedVersion = source.Version, ComparisonRevision = 1,
                Target = OpeningDiscrepancyAttribution.Incoming, PostedOn = new DateTime(2026, 10, 2), Reason = "Ajuste conferido"
            }, s.Manager.Token);
        Task<HttpResponseMessage> Lock() => client.PostAuthAsync("/setting/lock-month",
            new RequestLockSettingMonthJson { Year = 2026, Month = 10 }, s.Manager.Token, expectedVersion: setting.Version);
        var first = adjustmentFirst ? Adjust() : Lock();
        await held.WaitForWaitersAsync(1);
        var second = adjustmentFirst ? Lock() : Adjust();
        await held.WaitForWaitersAsync(2);
        await held.ReleaseAsync();
        var replies = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(15));
        var adjusted = replies[adjustmentFirst ? 0 : 1];
        var locked = replies[adjustmentFirst ? 1 : 0];
        locked.StatusCode.ShouldBe(HttpStatusCode.OK, await locked.Content.ReadAsStringAsync());
        adjusted.StatusCode.ShouldBe(adjustmentFirst ? HttpStatusCode.OK : HttpStatusCode.Conflict, await adjusted.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        (await db.OpeningDiscrepancyAdjustments.CountAsync(a => a.BranchId == s.Branch.Id)).ShouldBe(adjustmentFirst ? 1 : 0);
        (await db.Transactions.CountAsync(t => t.BranchId == s.Branch.Id)).ShouldBe(0);
        (await db.SessionOpeningDiscrepancies.AsNoTracking().SingleAsync(d => d.Id == source.Id)).Attribution.ShouldBe(OpeningDiscrepancyAttribution.Unattributed);
    }

    private async Task<(SessionScenario Scenario, ResponseOpeningDiscrepancyJson Source, ResponseSettingJson Setting)> StandaloneSource(
        FrozenClockScope clock, bool lockSource)
    {
        var (s, session) = await SessionCountScenario.StartWithOpeningAsync(factory, client, "ResponsibilityRace",
            new DateTime(2026, 9, 10), 980m);
        await factory.SeedSettingAsync(s.Branch.Id);
        var review = await (await client.GetAuthAsync($"/session/{session.Id}/review", s.Manager.Token)).ReadContentAsync<ResponseDailyCloseReviewJson>();
        var source = review.OpeningDiscrepancy.ShouldNotBeNull();
        await client.CountAsync(session.Id, s.MemberA.Token, new Dictionary<Guid, decimal> { [s.Cash.Id] = 980m });
        await client.SubmitAsync(session.Id, s.MemberA.Token);
        clock.AdvanceTo(new DateTime(2026, 9, 10, 9, 0, 0));
        await client.FinishAsync(session.Id, s.MemberA.Token);
        (await client.ApproveSessionAsync(session.Id, s.Manager.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        clock.AdvanceTo(new DateTime(2026, 10, 5, 10, 0, 0));
        var setting = await (await client.GetAuthAsync("/setting", s.Manager.Token)).ReadContentAsync<ResponseSettingJson>();
        if (lockSource)
        {
            var locked = await client.PostAuthAsync("/setting/lock-month", new RequestLockSettingMonthJson { Year = 2026, Month = 9 },
                s.Manager.Token, expectedVersion: setting.Version);
            locked.StatusCode.ShouldBe(HttpStatusCode.OK, await locked.Content.ReadAsStringAsync());
            setting = await locked.ReadContentAsync<ResponseSettingJson>();
        }
        return (s, source, setting);
    }

    [Fact]
    public async Task InitialOpening_HasNoOutgoingTarget_AndNoPersonalBlameUntilExplicitAttribution()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 9, 10, 8, 0, 0));
        var (s, session) = await SessionCountScenario.StartWithOpeningAsync(factory, client, "InitialResponsibility",
            new DateTime(2026, 9, 10), 980m);
        await factory.SeedSettingAsync(s.Branch.Id);
        var manager = s.Manager.Token;
        var review = await (await client.GetAuthAsync($"/session/{session.Id}/review", manager)).ReadContentAsync<ResponseDailyCloseReviewJson>();
        var source = review.OpeningDiscrepancy.ShouldNotBeNull();
        source.PredecessorSessionId.ShouldBeNull(); source.CurrentTotalDelta.ShouldBe(-20m);
        var ordinary = await client.PostAuthAsync($"/session/opening-discrepancy/{source.Id}/attribution",
            new RequestAttributeOpeningDiscrepancyJson { Target = OpeningDiscrepancyAttribution.Outgoing, DiscrepancyRevision = 1, Reason = "Sem predecessor" },
            manager, expectedVersion: source.Version);
        ordinary.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await client.CountAsync(session.Id, s.MemberA.Token, new Dictionary<Guid, decimal> { [s.Cash.Id] = 980m });
        await client.SubmitAsync(session.Id, s.MemberA.Token);
        clock.AdvanceTo(new DateTime(2026, 9, 10, 9, 0, 0));
        await client.FinishAsync(session.Id, s.MemberA.Token);
        (await client.ApproveSessionAsync(session.Id, manager)).StatusCode.ShouldBe(HttpStatusCode.OK);
        clock.AdvanceTo(new DateTime(2026, 10, 5, 10, 0, 0));
        var setting = await (await client.GetAuthAsync("/setting", manager)).ReadContentAsync<ResponseSettingJson>();
        var locked = await client.PostAuthAsync("/setting/lock-month", new RequestLockSettingMonthJson { Year = 2026, Month = 9 }, manager, expectedVersion: setting.Version);
        locked.StatusCode.ShouldBe(HttpStatusCode.OK, await locked.Content.ReadAsStringAsync());
        var context = await (await client.GetAuthAsync($"/session/opening-discrepancy/{source.Id}/responsibility", manager)).ReadContentAsync<ResponseOpeningResponsibilityJson>();
        context.Discrepancy.AllowedTargets.ShouldNotContain(OpeningDiscrepancyAttribution.Outgoing);
        foreach (var target in new[] { OpeningDiscrepancyAttribution.Outgoing, OpeningDiscrepancyAttribution.Neither })
        {
            var response = await client.PostAuthAsync("/session/opening-discrepancy-adjustment", new RequestOpeningDiscrepancyAdjustmentJson
            {
                DiscrepancyId = source.Id, ExpectedVersion = source.Version, ComparisonRevision = 1,
                Target = target, PostedOn = new DateTime(2026, 10, 2), Reason = "Conferido sem predecessor"
            }, manager);
            response.StatusCode.ShouldBe(target == OpeningDiscrepancyAttribution.Outgoing ? HttpStatusCode.BadRequest : HttpStatusCode.OK,
                await response.Content.ReadAsStringAsync());
        }
        var report = await (await client.GetAuthAsync("/report/personal-differences?year=2026&month=10", manager)).ReadContentAsync<ResponsePersonalDifferencesJson>();
        report.People.ShouldBeEmpty(); report.AutomaticPayrollDeduction.ShouldBeFalse();
        (await factory.ReloadAsync<SessionOpeningDiscrepancy>(source.Id))!.Attribution.ShouldBe(OpeningDiscrepancyAttribution.Unattributed);
    }

    [Fact]
    public async Task LockedStandaloneOpening_LateAttributionMovesAndClearsResponsibility_ExactlyOnceWithoutMoneyEffects()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 9, 10, 8, 0, 0));
        var s = await SessionScenario.CreateAsync(factory, "PersonalAdjustment");
        await factory.SeedSettingAsync(s.Branch.Id,dailyTargetHours:8,lunchDeductionOver6H:0,lunchDeductionOver4H:0);
        await factory.SeedTransactionTypeAsync(s.InCategory.Id,"Tarifa Especial sintética",systemRole:TransactionTypeSystemRole.SpecialFee,monthlyFinancialRole:MonthlyFinancialRole.SpecialDrawServiceFee,financialClass:FinancialClass.ShopFeesAndIncome);
        var manager = s.Manager.Token;
        var opening = await client.PostAuthAsync("/session/opening-count", new RequestRecordOpeningCountJson
        {
            AccountId = s.Cashbox.Id, AsOfDate = new DateTime(2026, 9, 10),
            Items = [new() { ProductId = s.Cash.Id, Value = 1000m }]
        }, manager);
        opening.StatusCode.ShouldBe(HttpStatusCode.OK, await opening.Content.ReadAsStringAsync());
        var first = await client.StartSessionAsync(s.Cashbox.Id, s.MemberA.Token);
        var cardCategory = await factory.SeedCategoryAsync(s.Branch.Id, defaultDirection: Direction.Out);
        var cardType = await factory.SeedTransactionTypeAsync(cardCategory.Id, settlementInstrument: SettlementInstrument.CreditCard,
            financialClass: FinancialClass.CaixaAndClientMoney);
        var claim = await factory.SeedTransactionAsync(s.Branch.Id, s.Cashbox.Id, cardType.Id, cardCategory.Id, Direction.Out,
            s.MemberA.Operator!.Id, s.Manager.User.Id, date: new DateTime(2026, 9, 10), value: 100m, sessionId: first.Session.Id);
        var countVersion=await client.GetSessionAsync(first.Session.Id,s.MemberA.Token);
        var feeCount=await client.PutAuthAsync($"/session/{first.Session.Id}/items",new VersionedRequestPutDailyCloseItemsJson
        {
            Version=countVersion.Version,Items=[new(){ProductId=s.Cash.Id,Value=915}],
            CloseFields=[new(){Field=SessionCloseField.SpecialFee,Value=15}]
        },s.MemberA.Token);
        feeCount.StatusCode.ShouldBe(HttpStatusCode.OK,await feeCount.Content.ReadAsStringAsync());
        await client.SubmitAsync(first.Session.Id, s.MemberA.Token);
        clock.AdvanceTo(new DateTime(2026, 9, 10, 9, 0, 0));
        await client.FinishAsync(first.Session.Id, s.MemberA.Token);
        (await client.ApproveSessionAsync(first.Session.Id, manager)).StatusCode.ShouldBe(HttpStatusCode.OK);
        clock.AdvanceTo(new DateTime(2026, 9, 10, 10, 0, 0));
        var second = await client.StartSessionAsync(s.Cashbox.Id, s.MemberB.Token,
            new Dictionary<Guid, decimal> { [s.Cash.Id] = 895m });
        var discrepancy = second.OpeningDiscrepancy.ShouldNotBeNull();
        discrepancy.CurrentTotalDelta.ShouldBe(-20m);
        discrepancy.HandoverId.ShouldBeNull();
        await client.CountAsync(second.Session.Id, s.MemberB.Token, new Dictionary<Guid, decimal> { [s.Cash.Id] = 890m });
        await client.SubmitAsync(second.Session.Id, s.MemberB.Token);
        clock.AdvanceTo(new DateTime(2026, 9, 10, 11, 0, 0));
        await client.FinishAsync(second.Session.Id, s.MemberB.Token);
        (await client.ApproveSessionAsync(second.Session.Id, manager)).StatusCode.ShouldBe(HttpStatusCode.OK);
        clock.AdvanceTo(new DateTime(2026,9,10,11,10,0));
        var third=await client.StartSessionAsync(s.Cashbox.Id,s.MemberA.Token);
        await client.CountAsync(third.Session.Id,s.MemberA.Token,new Dictionary<Guid,decimal>{[s.Cash.Id]=890});
        await client.SubmitAsync(third.Session.Id,s.MemberA.Token);
        clock.AdvanceTo(new DateTime(2026,9,10,11,20,0));
        await client.FinishAsync(third.Session.Id,s.MemberA.Token);
        (await client.ApproveSessionAsync(third.Session.Id,manager)).StatusCode.ShouldBe(HttpStatusCode.OK);
        clock.AdvanceTo(new DateTime(2026,9,30,12,0,0));
        var peopleDemo=new WebApi.Test.People.IntegratedPeopleDemo(factory,client,s.Branch.Id,s.MemberA.Operator!.Id,manager);
        await peopleDemo.SeedSeptember(first.Session.Id);
        var setting = await (await client.GetAuthAsync("/setting", manager)).ReadContentAsync<ResponseSettingJson>();
        var configure = await client.PutAuthAsync("/setting", new RequestUpdateSettingJson { PersonalShortageThreshold = 20m }, manager, setting.Version);
        configure.StatusCode.ShouldBe(HttpStatusCode.OK, await configure.Content.ReadAsStringAsync());
        setting = await configure.ReadContentAsync<ResponseSettingJson>();
        clock.AdvanceTo(new DateTime(2026, 10, 5, 10, 0, 0));
        var locked = await client.PostAuthAsync("/setting/lock-month", new RequestLockSettingMonthJson { Year = 2026, Month = 9 }, manager,
            expectedVersion: setting.Version);
        locked.StatusCode.ShouldBe(HttpStatusCode.OK, await locked.Content.ReadAsStringAsync());
        await peopleDemo.CrossOctober();
        await PostedCardJourney.ConfirmAsync(factory, client, s.Branch.Id, manager, claim);
        var settledResult = await Result(10);
        settledResult.KnownOwnResult.ShouldBe(-2m);
        settledResult.Classes.Single(c => c.FinancialClass == FinancialClass.InternalTransfer).KnownVolume.ShouldBe(100m);
        settledResult.Entries.Single(e => e.SourceKind == FinancialResultSourceKind.ProviderPrincipalSettlement).TransactionId.ShouldBe(claim.Id);
        var before = await Read(9);
        before.AutomaticPayrollDeduction.ShouldBeFalse();
        before.People.Single(p => p.OperatorId == s.MemberB.Operator!.Id).Shortage.ShouldBe(5m);
        before.UnassignedSourceCount.ShouldBe(1);
        before.Entries.Single(e => e.SourceId == discrepancy.Id).ShortageContribution.ShouldBe(0m);
        var original = (await factory.ReloadAsync<SessionOpeningDiscrepancy>(discrepancy.Id))!;
        var official = (await factory.ReloadAsync<DailyClose>(second.Session.Id))!;
        int ledgerCount;
        using (var scope = factory.Services.CreateScope())
            ledgerCount = await scope.ServiceProvider.GetRequiredService<ServerDbContext>().Transactions.CountAsync(t => t.BranchId == s.Branch.Id);
        var frozenResult = await Result(9);
        (await Post(Request(OpeningDiscrepancyAttribution.Outgoing, 0, null, date: new DateTime(2026, 9, 30)))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Post(Request(OpeningDiscrepancyAttribution.Outgoing, 0, null, comparison: 2))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Post(Request(OpeningDiscrepancyAttribution.Outgoing, 0, null, version: original.Version + 1))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var request = Request(OpeningDiscrepancyAttribution.Outgoing, 0, null);
        var key = Guid.NewGuid().ToString();
        // Actual overlapping HTTP requests share neither scoped DbContext nor mutable state.
        var replies = await Task.WhenAll(Post(request, key), Post(request, key));
        replies.ShouldAllBe(r => r.StatusCode == HttpStatusCode.OK);
        var a = await replies[0].ReadContentAsync<ResponseOpeningDiscrepancyAdjustmentJson>();
        (await replies[1].ReadContentAsync<ResponseOpeningDiscrepancyAdjustmentJson>()).Id.ShouldBe(a.Id);
        var noChange = await Post(Request(OpeningDiscrepancyAttribution.Outgoing, 1, a.Id));
        noChange.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await noChange.Content.ReadAsStringAsync()).ShouldContain(ResourcesErrorMessages.OPENING_DISCREPANCY_ADJUSTMENT_NO_CHANGE);
        var afterFirst = await Read(10);
        var personA = afterFirst.People.Single(p => p.OperatorId == s.MemberA.Operator!.Id);
        (personA.Shortage, personA.Overage, personA.SignedNet, personA.CurrentThresholdAlert).ShouldBe((20m, 0m, -20m, true));
        (await Read(10, "&asOfDate=2026-10-01")).TotalCount.ShouldBe(0);
        var changedKey = await Post(Request(OpeningDiscrepancyAttribution.Incoming, 0, null), key);
        changedKey.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var bRequest = Request(OpeningDiscrepancyAttribution.Incoming, 1, a.Id);
        var competing = await Task.WhenAll(Post(bRequest), Post(Request(OpeningDiscrepancyAttribution.Incoming, 1, a.Id)));
        competing.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        competing.Count(r => r.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        var b = await competing.Single(r => r.StatusCode == HttpStatusCode.OK).ReadContentAsync<ResponseOpeningDiscrepancyAdjustmentJson>();
        var cleared = await Post(Request(OpeningDiscrepancyAttribution.Neither, 2, b.Id));
        cleared.StatusCode.ShouldBe(HttpStatusCode.OK, await cleared.Content.ReadAsStringAsync());
        var after = await Read(10);
        after.People.Where(p => p.OperatorId != null).ShouldAllBe(p => p.Shortage == 0m && p.Overage == 0m && !p.CurrentThresholdAlert);
        (await Result(10)).KnownOwnResult.ShouldBe(-2m);
        var old = await Read(9);
        old.People.Single(p => p.OperatorId == s.MemberB.Operator!.Id).Shortage.ShouldBe(5m);
        (await Result(9)).KnownOwnResult.ShouldBe(frozenResult.KnownOwnResult);
        (await Result(9)).Classes.Single(c => c.FinancialClass == FinancialClass.CashDifference).KnownSignedTotal.ShouldBe(-5m);
        var root = (await factory.ReloadAsync<SessionOpeningDiscrepancy>(discrepancy.Id))!;
        (root.Version, root.CurrentRevision, root.Attribution, root.AttributedRevision).ShouldBe(
            (original.Version, original.CurrentRevision, original.Attribution, original.AttributedRevision));
        (await factory.ReloadAsync<DailyClose>(second.Session.Id))!.SubmittedAttemptId.ShouldBe(official.SubmittedAttemptId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            (await db.Transactions.CountAsync(t => t.BranchId == s.Branch.Id)).ShouldBe(ledgerCount);
            (await db.OpeningDiscrepancyAdjustments.CountAsync(a => a.BranchId == s.Branch.Id)).ShouldBe(3);
            (await db.BankReceipts.CountAsync(a => a.BranchId == s.Branch.Id)).ShouldBe(1);
            (await db.BankStatementMatches.CountAsync(a => a.BranchId == s.Branch.Id)).ShouldBe(1);
            (await db.SettlementEvents.CountAsync(a => a.BranchId == s.Branch.Id)).ShouldBe(1);
        }
        var contextResponse = await client.GetAuthAsync($"/session/opening-discrepancy/{discrepancy.Id}/responsibility?pageSize=2", manager);
        contextResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await contextResponse.Content.ReadAsStringAsync());
        var context = await contextResponse.ReadContentAsync<ResponseOpeningResponsibilityJson>();
        context.SourceLocked.ShouldBeTrue();
        context.Discrepancy.Version.ShouldBe(original.Version);
        context.Adjustments.Select(a => a.Sequence).ShouldBe([1, 2]);
        context.NextAfterSequence.ShouldBe(2);
        context.LatestAdjustment!.Sequence.ShouldBe(3);
        context.LatestAdjustment.ActorUserId.ShouldBe(s.Manager.User.Id);
        var tail = await (await client.GetAuthAsync($"/session/opening-discrepancy/{discrepancy.Id}/responsibility?afterSequence=2", manager))
            .ReadContentAsync<ResponseOpeningResponsibilityJson>();
        tail.Adjustments.ShouldHaveSingleItem().Sequence.ShouldBe(3);
        await peopleDemo.VerifyAfterResponsibilityCorrections();
        // Durable history is protected even from an accidental repository update.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var error = await Should.ThrowAsync<Npgsql.PostgresException>(() => db.OpeningDiscrepancyAdjustments
                .Where(a => a.Id == context.LatestAdjustment.Id).ExecuteUpdateAsync(set => set.SetProperty(a => a.Reason, "Changed")));
            error.SqlState.ShouldBe("23514");
        }
        var direct = await client.PostAuthAsync($"/session/opening-discrepancy/{discrepancy.Id}/attribution",
            new RequestAttributeOpeningDiscrepancyJson { DiscrepancyRevision = 1, Target = OpeningDiscrepancyAttribution.Incoming, Reason = "Não pode reescrever mês fechado" },
            manager, expectedVersion: root.Version);
        direct.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            await db.BranchUsers.Where(m => m.Id == s.Manager.Membership.Id).ExecuteUpdateAsync(set => set.SetProperty(m => m.Role, Role.Member));
        }
        // Exact successful replay must not escape revoked authority.
        (await Post(request, key)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        RequestOpeningDiscrepancyAdjustmentJson Request(OpeningDiscrepancyAttribution target, int sequence, Guid? previous, DateTime? date = null, int comparison = 1, uint? version = null) => new()
        {
            DiscrepancyId = discrepancy.Id, ExpectedVersion = version ?? original.Version, ComparisonRevision = comparison,
            ExpectedAdjustmentSequence = sequence, PreviousAdjustmentId = previous,
            Target = target, PostedOn = date ?? new DateTime(2026, 10, 2), Reason = "Responsabilidade revista com evidência"
        };
        Task<HttpResponseMessage> Post(RequestOpeningDiscrepancyAdjustmentJson value, string? requestKey = null) =>
            client.PostAuthAsync("/session/opening-discrepancy-adjustment", value, manager, idempotencyKey: requestKey);
        async Task<ResponsePersonalDifferencesJson> Read(int month, string query = "")
        {
            var response = await client.GetAuthAsync($"/report/personal-differences?year=2026&month={month}" + query, manager);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponsePersonalDifferencesJson>();
        }
        async Task<ResponseFinancialResultJson> Result(int month)
        {
            var response = await client.GetAuthAsync($"/report/financial-result?year=2026&month={month}", manager);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponseFinancialResultJson>();
        }
    }
}
