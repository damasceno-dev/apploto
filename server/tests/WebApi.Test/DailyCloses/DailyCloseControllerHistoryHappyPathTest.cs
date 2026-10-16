using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.DailyCloses;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

/// <summary>
/// M7.7 Phase 7.6.1 (gap X): durable close review events read through
/// <c>GET /dailyclose/{id}/history</c>. Every event here is produced by the real commands
/// (<c>PUT /items</c>, <c>/reject</c>), never seeded.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerHistoryHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RepeatedCorrections_ShouldRecordSeparateOpeningChanges_EvenAfterTheSuccessorIsDraft()
    {
        var ctx = await SeedContext("HistoryRepeatedCorrections");
        var source = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-5), DailyCloseStatus.Draft);
        await factory.SeedDailyCloseItemAsync(source.Id, ctx.ProductAId, 1000m);
        await factory.SeedDailyCloseItemAsync(source.Id, ctx.ProductBId, 200m);
        var successor = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-4), DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(successor.Id, ctx.ProductAId, 5m);

        var first = await PutItems(source.Id, ctx.ManagerToken, (ctx.ProductAId, 1100m), (ctx.ProductBId, 200m));
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        (await first.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).AffectedSuccessor!.DailyCloseId.ShouldBe(successor.Id);

        var second = await PutItems(source.Id, ctx.ManagerToken, (ctx.ProductAId, 1080m), (ctx.ProductBId, 200m));
        second.StatusCode.ShouldBe(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
        // The invalidation rule is unchanged: an already-Draft successor is not reported as demoted again.
        (await second.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).AffectedSuccessor.ShouldBeNull();

        var history = await GetHistory(successor.Id, ctx.ManagerToken);

        history.IsHistoryComplete.ShouldBeTrue();
        history.CurrentOpeningSource.ShouldNotBeNull();
        history.CurrentOpeningSource.DailyCloseId.ShouldBe(source.Id);
        history.Events.Count.ShouldBe(2);
        var firstEvent = history.Events[0];
        var secondEvent = history.Events[1];
        firstEvent.OccurredAt.ShouldBeLessThan(secondEvent.OccurredAt);
        foreach (var reviewEvent in history.Events)
        {
            reviewEvent.Type.ShouldBe(DailyCloseReviewEventType.OpeningChanged);
            reviewEvent.SourceDailyCloseId.ShouldBe(source.Id);
            reviewEvent.SourceDate.ShouldBe(ctx.Day(-5));
            reviewEvent.PreviousSourceDailyCloseId.ShouldBe(source.Id);
            reviewEvent.ActorUserId.ShouldBe(ctx.ManagerUserId);
            reviewEvent.ActorUserName.ShouldBe(ctx.ManagerUserName);
            reviewEvent.ActorOperatorId.ShouldBe(ctx.ManagerOperatorId);
            reviewEvent.ActorOperatorName.ShouldBe(ctx.ManagerOperatorName);
            reviewEvent.RejectionReason.ShouldBeNull();
        }

        firstEvent.AffectedStatusBefore.ShouldBe(DailyCloseStatus.Submitted);
        firstEvent.AffectedStatusAfter.ShouldBe(DailyCloseStatus.Draft);
        var firstItem = firstEvent.Items.ShouldHaveSingleItem();
        firstItem.ProductId.ShouldBe(ctx.ProductAId);
        firstItem.BeforeValue.ShouldBe(1000m);
        firstItem.AfterValue.ShouldBe(1100m);
        firstItem.Change.ShouldBe(100m);

        secondEvent.AffectedStatusBefore.ShouldBe(DailyCloseStatus.Draft);
        secondEvent.AffectedStatusAfter.ShouldBe(DailyCloseStatus.Draft);
        var secondItem = secondEvent.Items.ShouldHaveSingleItem();
        secondItem.BeforeValue.ShouldBe(1100m);
        secondItem.AfterValue.ShouldBe(1080m);
        secondItem.Change.ShouldBe(-20m);

        // The corrected source itself gets no event: only the affected successor's opening changed.
        (await GetHistory(source.Id, ctx.ManagerToken)).Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Rejection_ShouldKeepItsOwnEvent_WhenAnOpeningRecheckLaterDemotesTheClose()
    {
        var ctx = await SeedContext("HistoryRejectionAndRecheck");
        var source = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-5), DailyCloseStatus.Draft);
        await factory.SeedDailyCloseItemAsync(source.Id, ctx.ProductAId, 10m);
        var successor = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-4), DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(successor.Id, ctx.ProductAId, 5m);

        var reject = await _client.PostAuthAsync(
            $"/dailyclose/{successor.Id}/reject",
            new RequestRejectDailyCloseJson { RejectionReason = "Contagem de bilhetes divergente" },
            ctx.ManagerToken);
        reject.StatusCode.ShouldBe(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());
        var rejected = await reject.ReadContentAsync<ResponseDailyCloseJson>();

        var correction = await PutItems(source.Id, ctx.ManagerToken, (ctx.ProductAId, 20m));
        correction.StatusCode.ShouldBe(HttpStatusCode.OK, await correction.Content.ReadAsStringAsync());

        var history = await GetHistory(successor.Id, ctx.ManagerToken);

        history.Events.Count.ShouldBe(2);
        var rejection = history.Events[0];
        rejection.Type.ShouldBe(DailyCloseReviewEventType.Rejected);
        rejection.OccurredAt.ShouldBe(rejected.UpdatedAt!.Value, TimeSpan.FromMilliseconds(1));
        rejection.RejectionReason.ShouldBe("Contagem de bilhetes divergente");
        rejection.ActorUserName.ShouldBe(ctx.ManagerUserName);
        rejection.ActorOperatorName.ShouldBe(ctx.ManagerOperatorName);
        rejection.SourceDailyCloseId.ShouldBeNull();
        rejection.Items.ShouldBeEmpty();
        var openingChange = history.Events[1];
        openingChange.Type.ShouldBe(DailyCloseReviewEventType.OpeningChanged);
        openingChange.AffectedStatusBefore.ShouldBe(DailyCloseStatus.Rejected);
        openingChange.AffectedStatusAfter.ShouldBe(DailyCloseStatus.Draft);

        // The latest-only fields have moved on; the rejection event still carries its own time.
        var reloaded = await factory.ReloadAsync<DailyClose>(successor.Id);
        reloaded!.Status.ShouldBe(DailyCloseStatus.Draft);
        reloaded.RejectionReason.ShouldBe("Contagem de bilhetes divergente");
        reloaded.UpdatedAt.ShouldNotBe(rejected.UpdatedAt);
    }

    [Fact]
    public async Task FirstCount_ShouldRecordTheReplacedSource_KeepingMissingItemsDistinctFromZero()
    {
        var ctx = await SeedContext("HistoryFirstCount");
        var oldSource = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-6), DailyCloseStatus.Approved);
        await factory.SeedDailyCloseItemAsync(oldSource.Id, ctx.ProductAId, 500m);
        await factory.SeedDailyCloseItemAsync(oldSource.Id, ctx.ProductBId, 0m);
        var uncounted = await factory.SeedDailyCloseAsync(
            ctx.BranchId, ctx.AccountId, ctx.Day(-5), DailyCloseStatus.Draft, itemsRecorded: false);
        var successor = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-4), DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(successor.Id, ctx.ProductAId, 5m);

        // First count: A unchanged, B (a recorded zero before) omitted, C new.
        var firstCount = await PutItems(uncounted.Id, ctx.ManagerToken, (ctx.ProductAId, 500m), (ctx.ProductCId, 30m));
        firstCount.StatusCode.ShouldBe(HttpStatusCode.OK, await firstCount.Content.ReadAsStringAsync());

        var history = await GetHistory(successor.Id, ctx.ManagerToken);

        var reviewEvent = history.Events.ShouldHaveSingleItem();
        reviewEvent.Type.ShouldBe(DailyCloseReviewEventType.OpeningChanged);
        reviewEvent.SourceDailyCloseId.ShouldBe(uncounted.Id);
        reviewEvent.PreviousSourceDailyCloseId.ShouldBe(oldSource.Id);
        reviewEvent.PreviousSourceDate.ShouldBe(ctx.Day(-6));
        reviewEvent.AffectedStatusBefore.ShouldBe(DailyCloseStatus.Submitted);
        reviewEvent.AffectedStatusAfter.ShouldBe(DailyCloseStatus.Draft);
        reviewEvent.Items.Count.ShouldBe(2);
        var productB = reviewEvent.Items.Single(item => item.ProductId == ctx.ProductBId);
        productB.BeforeValue.ShouldBe(0m);
        productB.AfterValue.ShouldBeNull();
        productB.Change.ShouldBe(0m);
        var productC = reviewEvent.Items.Single(item => item.ProductId == ctx.ProductCId);
        productC.BeforeValue.ShouldBeNull();
        productC.AfterValue.ShouldBe(30m);
        productC.Change.ShouldBe(30m);
        history.CurrentOpeningSource!.DailyCloseId.ShouldBe(uncounted.Id);
    }

    [Fact]
    public async Task Correction_ShouldRecordOnlyOnTheNextEligibleSuccessor_AndNothingWithoutACountChange()
    {
        var ctx = await SeedContext("HistoryScope");
        var source = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-6), DailyCloseStatus.Draft);
        await factory.SeedDailyCloseItemAsync(source.Id, ctx.ProductAId, 100m);
        var uncountedBetween = await factory.SeedDailyCloseAsync(
            ctx.BranchId, ctx.AccountId, ctx.Day(-5), DailyCloseStatus.Draft, itemsRecorded: false);
        var successor = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-4), DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(successor.Id, ctx.ProductAId, 5m);
        var later = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-3), DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(later.Id, ctx.ProductAId, 5m);
        var otherAccount = await factory.SeedAccountAsync(ctx.BranchId, AccountType.Terminal);
        var otherAccountClose = await factory.SeedDailyCloseAsync(ctx.BranchId, otherAccount.Id, ctx.Day(-4), DailyCloseStatus.Submitted);

        var unchanged = await PutItems(source.Id, ctx.ManagerToken, (ctx.ProductAId, 100m));
        unchanged.StatusCode.ShouldBe(HttpStatusCode.OK, await unchanged.Content.ReadAsStringAsync());
        (await GetHistory(successor.Id, ctx.ManagerToken)).Events.ShouldBeEmpty();

        var changed = await PutItems(source.Id, ctx.ManagerToken, (ctx.ProductAId, 150m));
        changed.StatusCode.ShouldBe(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync());

        (await GetHistory(successor.Id, ctx.ManagerToken)).Events.ShouldHaveSingleItem()
            .Type.ShouldBe(DailyCloseReviewEventType.OpeningChanged);
        (await GetHistory(uncountedBetween.Id, ctx.ManagerToken)).Events.ShouldBeEmpty();
        (await GetHistory(later.Id, ctx.ManagerToken)).Events.ShouldBeEmpty();
        (await GetHistory(otherAccountClose.Id, ctx.ManagerToken)).Events.ShouldBeEmpty();
        (await factory.ReloadAsync<DailyClose>(later.Id))!.Status.ShouldBe(DailyCloseStatus.Submitted);
        (await factory.ReloadAsync<DailyClose>(otherAccountClose.Id))!.Status.ShouldBe(DailyCloseStatus.Submitted);
    }

    [Fact]
    public async Task History_ShouldExposeActorNamesToAScopedMember()
    {
        var ctx = await SeedContext("HistoryMemberRead");
        var successor = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-4), DailyCloseStatus.Submitted);
        var reject = await _client.PostAuthAsync(
            $"/dailyclose/{successor.Id}/reject",
            new RequestRejectDailyCloseJson { RejectionReason = "Recontar" },
            ctx.ManagerToken);
        reject.StatusCode.ShouldBe(HttpStatusCode.OK, await reject.Content.ReadAsStringAsync());

        var history = await GetHistory(successor.Id, ctx.MemberToken);

        var rejection = history.Events.ShouldHaveSingleItem();
        rejection.ActorUserName.ShouldBe(ctx.ManagerUserName);
        rejection.ActorOperatorName.ShouldBe(ctx.ManagerOperatorName);
        rejection.RejectionReason.ShouldBe("Recontar");
    }

    [Fact]
    public async Task History_ShouldReportIncompleteHistoryWithoutInventedEvents_ForALegacyClose()
    {
        var ctx = await SeedContext("HistoryLegacy");
        var legacy = await factory.SeedDailyCloseAsync(
            ctx.BranchId, ctx.AccountId, ctx.Day(-4), DailyCloseStatus.Rejected, rejectionReason: "Rejeitado antes dos eventos");
        await MarkHistoryIncomplete(legacy.Id);
        var fresh = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Day(-3), DailyCloseStatus.Draft);

        var legacyHistory = await GetHistory(legacy.Id, ctx.ManagerToken);
        var freshHistory = await GetHistory(fresh.Id, ctx.ManagerToken);

        legacyHistory.IsHistoryComplete.ShouldBeFalse();
        legacyHistory.Events.ShouldBeEmpty();
        legacyHistory.CurrentOpeningSource.ShouldBeNull();
        freshHistory.IsHistoryComplete.ShouldBeTrue();
        freshHistory.Events.ShouldBeEmpty();
    }

    private async Task<HttpResponseMessage> PutItems(Guid dailyCloseId, string token, params (Guid ProductId, decimal Value)[] items)
    {
        var current = await _client.GetAuthAsync($"/dailyclose/{dailyCloseId}", token);
        current.StatusCode.ShouldBe(HttpStatusCode.OK, await current.Content.ReadAsStringAsync());
        var version = (await current.ReadContentAsync<ResponseDailyCloseJson>()).Version;
        return await _client.PutAuthAsync(
            $"/dailyclose/{dailyCloseId}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = version,
                Items = [.. items.Select(item => new RequestUpsertDailyCloseItemJson { ProductId = item.ProductId, Value = item.Value })]
            },
            token);
    }

    private async Task<ResponseDailyCloseHistoryJson> GetHistory(Guid dailyCloseId, string token)
    {
        var response = await _client.GetAuthAsync($"/dailyclose/{dailyCloseId}/history", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseDailyCloseHistoryJson>();
    }

    private async Task MarkHistoryIncomplete(Guid dailyCloseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "DailyCloses" SET "ReviewHistoryComplete" = FALSE WHERE "Id" = {dailyCloseId}""");
    }

    private async Task<HistoryContext> SeedContext(string label)
    {
        var (managerUser, branch, _, managerToken) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var managerOperator = await factory.SeedOperatorAsync(branch.Id, $"Gerente {label}", userId: managerUser.Id);
        var memberUser = await factory.SeedUserAsync();
        var memberMembership = await factory.SeedBranchUserAsync(memberUser.Id, branch.Id, Role.Member);
        var memberOperator = await factory.SeedOperatorAsync(branch.Id, userId: memberUser.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(memberOperator.Id, account.Id, isPrimary: true);
        var productA = await factory.SeedProductAsync(branch.Id, displayOrder: 1);
        var productB = await factory.SeedProductAsync(branch.Id, displayOrder: 2);
        var productC = await factory.SeedProductAsync(branch.Id, displayOrder: 3);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName, displayOrder: 99);
        return new HistoryContext(
            branch.Id,
            account.Id,
            productA.Id,
            productB.Id,
            productC.Id,
            managerUser.Id,
            managerUser.Name,
            managerOperator.Id,
            managerOperator.Name,
            managerToken,
            factory.IssueBranchToken(memberMembership),
            new BranchClock().LocalBusinessDate(DateTime.UtcNow));
    }

    private sealed record HistoryContext(
        Guid BranchId,
        Guid AccountId,
        Guid ProductAId,
        Guid ProductBId,
        Guid ProductCId,
        Guid ManagerUserId,
        string ManagerUserName,
        Guid ManagerOperatorId,
        string ManagerOperatorName,
        string ManagerToken,
        string MemberToken,
        DateTime Today)
    {
        public DateTime Day(int offset) => Today.AddDays(offset);
    }
}
