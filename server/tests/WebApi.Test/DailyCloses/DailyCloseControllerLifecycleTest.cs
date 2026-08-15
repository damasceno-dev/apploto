using System.Net;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerLifecycleTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task RejectCycle_ShouldResubmitAndApproveWithCashVarianceUpdatedInPlace()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcLifecycleReject", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id);
        var cvProduct = await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);

        var opened = await OpenAsync(account.Id, LocalToday(), token);
        var openReload = await factory.ReloadAsync<DailyClose>(opened.Id);
        openReload.ShouldNotBeNull();
        openReload.Status.ShouldBe(DailyCloseStatus.Draft);

        await PutItemsAsync(opened.Id, product.Id, 100m, token);
        var afterFirstPut = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterFirstPut.ShouldNotBeNull();
        afterFirstPut.Status.ShouldBe(DailyCloseStatus.Draft);
        afterFirstPut.UpdatedAt.ShouldNotBeNull();

        await SubmitAsync(opened.Id, token);
        var afterFirstSubmit = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterFirstSubmit.ShouldNotBeNull();
        afterFirstSubmit.Status.ShouldBe(DailyCloseStatus.Submitted);
        afterFirstSubmit.SubmittedAt.ShouldNotBeNull();
        var firstCashVariance = await GetCashVarianceAsync(opened.Id, cvProduct.Id);
        firstCashVariance.Value.ShouldBe(100m);

        await RejectAsync(opened.Id, "test", token);
        var afterReject = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterReject.ShouldNotBeNull();
        afterReject.Status.ShouldBe(DailyCloseStatus.Rejected);
        afterReject.RejectionReason.ShouldBe("test");
        afterReject.UpdatedAt.ShouldNotBeNull();

        await PutItemsAsync(opened.Id, product.Id, 150m, token);
        var afterSecondPut = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterSecondPut.ShouldNotBeNull();
        afterSecondPut.Status.ShouldBe(DailyCloseStatus.Draft);
        afterSecondPut.UpdatedAt.ShouldNotBeNull();

        await SubmitAsync(opened.Id, token);
        var afterSecondSubmit = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterSecondSubmit.ShouldNotBeNull();
        afterSecondSubmit.Status.ShouldBe(DailyCloseStatus.Submitted);
        afterSecondSubmit.SubmittedAt.ShouldNotBeNull();
        var secondCashVariance = await GetCashVarianceAsync(opened.Id, cvProduct.Id);
        secondCashVariance.Id.ShouldBe(firstCashVariance.Id);
        secondCashVariance.Value.ShouldBe(150m);

        await ApproveAsync(opened.Id, token);
        var afterApprove = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterApprove.ShouldNotBeNull();
        afterApprove.Status.ShouldBe(DailyCloseStatus.Approved);
        afterApprove.ApprovedAt.ShouldNotBeNull();
        afterApprove.ApprovedByUserId.ShouldNotBeNull();
    }

    [Fact]
    public async Task RecallCycle_ShouldRecallSameDayMemberCloseAndUpdateCashVarianceInPlace()
    {
        var (member, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("DcLifecycleRecall", Role.Member);
        var manager = await factory.SeedUserAsync();
        var managerMembership = await factory.SeedBranchUserAsync(manager.Id, branch.Id, Role.Manager);
        var managerToken = factory.IssueBranchToken(managerMembership);

        var op = await factory.SeedOperatorAsync(branch.Id, userId: member.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var product = await factory.SeedProductAsync(branch.Id);
        var cvProduct = await factory.SeedProductAsync(branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);

        var opened = await OpenAsync(account.Id, LocalToday(), memberToken);
        var openReload = await factory.ReloadAsync<DailyClose>(opened.Id);
        openReload.ShouldNotBeNull();
        openReload.Status.ShouldBe(DailyCloseStatus.Draft);
        openReload.RecordedByUserId.ShouldBeNull();
        openReload.RecordedByOperatorId.ShouldBeNull();
        openReload.SubmittedByUserId.ShouldBeNull();
        openReload.SubmittedByOperatorId.ShouldBeNull();

        await PutItemsAsync(opened.Id, product.Id, 100m, memberToken);
        var afterFirstPut = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterFirstPut.ShouldNotBeNull();
        afterFirstPut.Status.ShouldBe(DailyCloseStatus.Draft);
        afterFirstPut.RecordedByUserId.ShouldBe(member.Id);
        afterFirstPut.RecordedByOperatorId.ShouldBe(op.Id);
        afterFirstPut.UpdatedAt.ShouldNotBeNull();

        await SubmitAsync(opened.Id, memberToken);
        var afterFirstSubmit = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterFirstSubmit.ShouldNotBeNull();
        afterFirstSubmit.Status.ShouldBe(DailyCloseStatus.Submitted);
        afterFirstSubmit.SubmittedAt.ShouldNotBeNull();
        afterFirstSubmit.SubmittedByOperatorId.ShouldBe(op.Id);
        var firstCashVariance = await GetCashVarianceAsync(opened.Id, cvProduct.Id);
        firstCashVariance.Value.ShouldBe(100m);

        var ordinaryItemSave = await _client.PutAuthAsync(
            $"/dailyclose/{opened.Id}/items",
            new RequestPutDailyCloseItemsJson
            {
                Version = afterFirstSubmit.Version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 150m }]
            },
            memberToken);
        ordinaryItemSave.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var stillFrozen = await factory.ReloadAsync<DailyClose>(opened.Id);
        stillFrozen.ShouldNotBeNull();
        stillFrozen.Status.ShouldBe(DailyCloseStatus.Submitted);
        stillFrozen.SubmittedAt.ShouldNotBeNull();

        await RecallAsync(opened.Id, memberToken);
        await PutItemsAsync(opened.Id, product.Id, 150m, memberToken);
        var afterRecallPut = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterRecallPut.ShouldNotBeNull();
        afterRecallPut.Status.ShouldBe(DailyCloseStatus.Draft);
        afterRecallPut.RecordedByUserId.ShouldBe(member.Id);
        afterRecallPut.RecordedByOperatorId.ShouldBe(op.Id);
        afterRecallPut.SubmittedByUserId.ShouldBeNull();
        afterRecallPut.SubmittedByOperatorId.ShouldBeNull();
        afterRecallPut.SubmittedAt.ShouldBeNull();
        afterRecallPut.UpdatedAt.ShouldNotBeNull();
        var unchangedCashVariance = await GetCashVarianceAsync(opened.Id, cvProduct.Id);
        unchangedCashVariance.Id.ShouldBe(firstCashVariance.Id);
        unchangedCashVariance.Value.ShouldBe(100m);

        await SubmitAsync(opened.Id, memberToken);
        var afterSecondSubmit = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterSecondSubmit.ShouldNotBeNull();
        afterSecondSubmit.Status.ShouldBe(DailyCloseStatus.Submitted);
        afterSecondSubmit.SubmittedAt.ShouldNotBeNull();
        afterSecondSubmit.SubmittedByOperatorId.ShouldBe(op.Id);
        var secondCashVariance = await GetCashVarianceAsync(opened.Id, cvProduct.Id);
        secondCashVariance.Id.ShouldBe(firstCashVariance.Id);
        secondCashVariance.Value.ShouldBe(150m);

        await ApproveAsync(opened.Id, managerToken);
        var afterApprove = await factory.ReloadAsync<DailyClose>(opened.Id);
        afterApprove.ShouldNotBeNull();
        afterApprove.Status.ShouldBe(DailyCloseStatus.Approved);
        afterApprove.ApprovedAt.ShouldNotBeNull();
        afterApprove.ApprovedByUserId.ShouldBe(manager.Id);
    }

    private async Task<ResponseDailyCloseJson> OpenAsync(Guid accountId, DateTime date, string token)
    {
        var response = await _client.PostAuthAsync(
            "/dailyclose",
            new RequestOpenDailyCloseJson { AccountId = accountId, Date = date },
            token);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadContentAsync<ResponseDailyCloseJson>();
    }

    private async Task PutItemsAsync(Guid closeId, Guid productId, decimal value, string token)
    {
        var close = await factory.ReloadAsync<DailyClose>(closeId);
        close.ShouldNotBeNull();
        var response = await _client.PutAuthAsync(
            $"/dailyclose/{closeId}/items",
            new RequestPutDailyCloseItemsJson
            {
                Version = close.Version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = productId, Value = value }]
            },
            token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SubmitAsync(Guid closeId, string token)
    {
        var response = await _client.PostAuthAsync($"/dailyclose/{closeId}/submit", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task RejectAsync(Guid closeId, string rejectionReason, string token)
    {
        var response = await _client.PostAuthAsync(
            $"/dailyclose/{closeId}/reject",
            new RequestRejectDailyCloseJson { RejectionReason = rejectionReason },
            token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task RecallAsync(Guid closeId, string token)
    {
        var response = await _client.PostAuthAsync($"/dailyclose/{closeId}/recall", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task ApproveAsync(Guid closeId, string token)
    {
        var response = await _client.PostAuthAsync($"/dailyclose/{closeId}/approve", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<DailyCloseItem> GetCashVarianceAsync(Guid closeId, Guid cvProductId)
    {
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(closeId);
        return items.Single(item => item.ProductId == cvProductId);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
