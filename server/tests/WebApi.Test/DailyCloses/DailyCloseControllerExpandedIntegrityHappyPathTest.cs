using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerExpandedIntegrityHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task EmptySave_ShouldEstablishFirstCountAndAllowRetryAfterNoCountSubmit()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcExpandedEmptyCount",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            LocalToday(),
            itemsRecorded: false);

        var rejectedSubmit = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        rejectedSubmit.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await rejectedSubmit.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEMS_NOT_RECORDED);
        var rejectedState = await factory.ReloadAsync<DailyClose>(close.Id);
        rejectedState.ShouldNotBeNull();
        rejectedState.Status.ShouldBe(DailyCloseStatus.Draft);
        rejectedState.ItemsFirstRecordedAt.ShouldBeNull();
        (await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id)).ShouldBeEmpty();

        var saveResponse = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson { Version = rejectedState.Version, Items = [] },
            token);

        saveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var saved = await saveResponse.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        saved.DailyClose.ItemsFirstRecordedAt.ShouldNotBeNull();
        saved.DailyClose.Items.ShouldBeEmpty();
        saved.AffectedSuccessor.ShouldBeNull();

        var submitResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);

        submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var submitted = await submitResponse.ReadContentAsync<ResponseDailyCloseJson>();
        submitted.Status.ShouldBe(DailyCloseStatus.Submitted);
        submitted.ItemsFirstRecordedAt.ShouldBe(saved.DailyClose.ItemsFirstRecordedAt);
        submitted.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task ActualChange_ShouldDemoteOnlyEarliestLaterEligibleOfficialClose()
    {
        var context = await SeedCascadeContextAsync(
            "DcExpandedOneHop",
            DailyCloseStatus.Approved,
            includeInterveningNeverCounted: true);

        var response = await PutAsync(
            context.Predecessor,
            context.Token,
            [(context.ProductA.Id, 125m)]);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        payload.AffectedSuccessor.ShouldNotBeNull();
        payload.AffectedSuccessor.DailyCloseId.ShouldBe(context.Successor.Id);
        payload.AffectedSuccessor.Date.ShouldBe(context.Successor.Date);
        payload.AffectedSuccessor.PreviousStatus.ShouldBe(DailyCloseStatus.Approved);
        payload.AffectedSuccessor.NewStatus.ShouldBe(DailyCloseStatus.Draft);
        payload.AffectedSuccessor.OpeningRecheckRequiredAt.ShouldNotBe(default);

        var successor = await factory.ReloadAsync<DailyClose>(context.Successor.Id);
        successor.ShouldNotBeNull();
        successor.Status.ShouldBe(DailyCloseStatus.Draft);
        successor.OpeningRecheckTriggeredByDailyCloseId.ShouldBe(context.Predecessor.Id);
        successor.OpeningRecheckTriggeredByUserId.ShouldBe(context.UserId);
        successor.ItemsFirstRecordedAt.ShouldNotBeNull();

        var untouchedLater = await factory.ReloadAsync<DailyClose>(context.LaterSuccessor.Id);
        untouchedLater.ShouldNotBeNull();
        untouchedLater.Status.ShouldBe(DailyCloseStatus.Submitted);
        untouchedLater.OpeningRecheckRequiredAt.ShouldBeNull();

        context.InterveningNeverCounted.ShouldNotBeNull();
        var ineligible = await factory.ReloadAsync<DailyClose>(context.InterveningNeverCounted.Id);
        ineligible.ShouldNotBeNull();
        ineligible.Status.ShouldBe(DailyCloseStatus.Submitted);
        ineligible.ItemsFirstRecordedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(ActualMapShapeChange.Add)]
    [InlineData(ActualMapShapeChange.Remove)]
    public async Task ActiveItemMapShapeChange_ShouldCascade(ActualMapShapeChange change)
    {
        var context = await SeedCascadeContextAsync(
            $"DcExpandedShape{change}",
            DailyCloseStatus.Submitted,
            includeInterveningNeverCounted: false,
            includeSecondProduct: true,
            seedSecondProductItem: change == ActualMapShapeChange.Remove);
        var items = change == ActualMapShapeChange.Add
            ? new[] { (context.ProductA.Id, 100m), (context.ProductB!.Id, 40m) }
            : new[] { (context.ProductA.Id, 100m) };

        var response = await PutAsync(context.Predecessor, context.Token, items);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        payload.AffectedSuccessor.ShouldNotBeNull();
        payload.AffectedSuccessor.DailyCloseId.ShouldBe(context.Successor.Id);
        payload.AffectedSuccessor.PreviousStatus.ShouldBe(DailyCloseStatus.Submitted);
        payload.AffectedSuccessor.NewStatus.ShouldBe(DailyCloseStatus.Draft);
    }

    [Theory]
    [InlineData(NoOpSave.Identical)]
    [InlineData(NoOpSave.Reordered)]
    [InlineData(NoOpSave.NotesOnly)]
    [InlineData(NoOpSave.Retry)]
    public async Task NoOpSave_ShouldNotCascade(NoOpSave save)
    {
        var context = await SeedCascadeContextAsync(
            $"DcExpandedNoOp{save}",
            DailyCloseStatus.Submitted,
            includeInterveningNeverCounted: false,
            includeSecondProduct: true);
        var items = save == NoOpSave.Reordered
            ? new[] { (context.ProductB!.Id, 40m), (context.ProductA.Id, 100m) }
            : new[] { (context.ProductA.Id, 100m), (context.ProductB!.Id, 40m) };

        var first = await PutAsync(
            context.Predecessor,
            context.Token,
            items,
            save == NoOpSave.NotesOnly ? "notes changed" : null);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPayload = await first.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        firstPayload.AffectedSuccessor.ShouldBeNull();

        if (save == NoOpSave.Retry)
        {
            var retry = await PutAsync(
                context.Predecessor,
                context.Token,
                items,
                null,
                firstPayload.DailyClose.Version);
            retry.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await retry.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).AffectedSuccessor.ShouldBeNull();
        }

        var successor = await factory.ReloadAsync<DailyClose>(context.Successor.Id);
        successor.ShouldNotBeNull();
        successor.Status.ShouldBe(DailyCloseStatus.Submitted);
        successor.OpeningRecheckRequiredAt.ShouldBeNull();
    }

    [Fact]
    public async Task FirstEligibilitySave_ShouldCascadeEvenWhenExplicitlyEmpty()
    {
        var context = await SeedCascadeContextAsync(
            "DcExpandedFirstCountCascade",
            DailyCloseStatus.Submitted,
            includeInterveningNeverCounted: false,
            predecessorCounted: false);

        var response = await PutAsync(context.Predecessor, context.Token, []);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await response.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        payload.DailyClose.ItemsFirstRecordedAt.ShouldNotBeNull();
        payload.AffectedSuccessor.ShouldNotBeNull();
        payload.AffectedSuccessor.DailyCloseId.ShouldBe(context.Successor.Id);
    }

    [Fact]
    public async Task RejectedSuccessor_ShouldRetainReasonWhenOpeningRecheckIsRequired()
    {
        var context = await SeedCascadeContextAsync(
            "DcExpandedRejectedRetention",
            DailyCloseStatus.Rejected,
            includeInterveningNeverCounted: false);

        var response = await PutAsync(
            context.Predecessor,
            context.Token,
            [(context.ProductA.Id, 101m)]);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var successor = await factory.ReloadAsync<DailyClose>(context.Successor.Id);
        successor.ShouldNotBeNull();
        successor.Status.ShouldBe(DailyCloseStatus.Draft);
        successor.RejectionReason.ShouldBe("Retain this rejection reason");
        successor.OpeningRecheckRequiredAt.ShouldNotBeNull();
    }

    private async Task<CascadeContext> SeedCascadeContextAsync(
        string label,
        DailyCloseStatus successorStatus,
        bool includeInterveningNeverCounted,
        bool includeSecondProduct = false,
        bool predecessorCounted = true,
        bool seedSecondProductItem = true)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var productA = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        var productB = includeSecondProduct
            ? await factory.SeedProductAsync(branch.Id, displayOrder: 20)
            : null;
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 99);
        var date = LocalToday().AddDays(-5);
        var predecessor = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            DailyCloseStatus.Draft,
            itemsRecorded: predecessorCounted);
        if (predecessorCounted)
        {
            await factory.SeedDailyCloseItemAsync(predecessor.Id, productA.Id, 100m);
            if (productB is not null && seedSecondProductItem)
                await factory.SeedDailyCloseItemAsync(predecessor.Id, productB.Id, 40m);
        }

        DailyClose? intervening = null;
        if (includeInterveningNeverCounted)
        {
            intervening = await factory.SeedDailyCloseAsync(
                branch.Id,
                account.Id,
                date.AddDays(1),
                DailyCloseStatus.Submitted,
                itemsRecorded: false);
        }

        var successor = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date.AddDays(2),
            successorStatus,
            rejectionReason: successorStatus == DailyCloseStatus.Rejected
                ? "Retain this rejection reason"
                : null);
        await factory.SeedDailyCloseItemAsync(successor.Id, productA.Id, 80m);
        var laterSuccessor = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date.AddDays(3),
            DailyCloseStatus.Submitted);
        await factory.SeedDailyCloseItemAsync(laterSuccessor.Id, productA.Id, 70m);

        return new CascadeContext(
            user.Id,
            token,
            predecessor,
            successor,
            laterSuccessor,
            intervening,
            productA,
            productB);
    }

    private Task<HttpResponseMessage> PutAsync(
        DailyClose close,
        string token,
        IReadOnlyList<(Guid ProductId, decimal Value)> items,
        string? notes = null,
        uint? version = null)
    {
        return _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = version ?? close.Version,
                Notes = notes,
                Items = items.Select(item => new RequestUpsertDailyCloseItemJson
                {
                    ProductId = item.ProductId,
                    Value = item.Value
                }).ToList()
            },
            token);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }

    public enum NoOpSave
    {
        Identical,
        Reordered,
        NotesOnly,
        Retry
    }

    public enum ActualMapShapeChange
    {
        Add,
        Remove
    }

    private sealed record CascadeContext(
        Guid UserId,
        string Token,
        DailyClose Predecessor,
        DailyClose Successor,
        DailyClose LaterSuccessor,
        DailyClose? InterveningNeverCounted,
        Product ProductA,
        Product? ProductB);
}
