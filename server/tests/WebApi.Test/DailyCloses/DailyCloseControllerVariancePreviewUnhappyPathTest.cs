using System.Net;
using System.Net.Http.Json;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerVariancePreviewUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task VariancePreview_ShouldReturn401_WhenTokenIsMissing()
    {
        var response = await _client.PostAsync(
            $"/dailyclose/{Guid.NewGuid()}/variance-preview",
            JsonContent.Create(new RequestDailyCloseVariancePreviewJson { Items = [] }));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(DailyCloseStatus.Submitted)]
    [InlineData(DailyCloseStatus.Approved)]
    public async Task VariancePreview_ShouldReturn409_WhenStatusIsNotSubmittable(
        DailyCloseStatus status)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcVariancePreviewStatus{status}",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, status: status);

        var response = await PostAsync(
            close.Id,
            [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 10m }],
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await response.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_SUBMITTABLE);
    }

    [Fact]
    public async Task VariancePreview_ShouldReturn403_WhenMemberHasNoLinkedOperator()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcVariancePreviewNoLink",
            Role.Member);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);

        var response = await PostAsync(
            close.Id,
            [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 10m }],
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await response.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(
            ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public async Task VariancePreview_ShouldReturn403_WhenMemberAccountIsOutOfScope()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcVariancePreviewScope",
            Role.Member);
        var memberOperator = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var allowedAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(memberOperator.Id, allowedAccount.Id);
        var closeAccount = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            closeAccount.Id,
            submittedByOperatorId: memberOperator.Id);

        var response = await PostAsync(
            close.Id,
            [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 10m }],
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await response.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(
            ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public async Task VariancePreview_ShouldReturn404_WhenCloseBelongsToAnotherBranch()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync(
            "DcVariancePreviewCrossBranch",
            Role.Admin);
        var otherBranch = await factory.SeedBranchForOtherContextAsync(
            "DcVariancePreviewCrossBranchOther");
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);
        var otherClose = await factory.SeedDailyCloseAsync(otherBranch.Id, otherAccount.Id);

        var response = await PostAsync(
            otherClose.Id,
            [new RequestUpsertDailyCloseItemJson { ProductId = Guid.NewGuid(), Value = 10m }],
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await response.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
    }

    [Fact]
    public async Task VariancePreview_ShouldReturn400_WhenCandidateContainsVarianceProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcVariancePreviewReserved",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var varianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);

        var response = await PostAsync(
            close.Id,
            [new RequestUpsertDailyCloseItemJson { ProductId = varianceProduct.Id, Value = 10m }],
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await response.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN);
    }

    private Task<HttpResponseMessage> PostAsync(
        Guid closeId,
        IReadOnlyList<RequestUpsertDailyCloseItemJson> items,
        string token)
    {
        return _client.PostAuthAsync(
            $"/dailyclose/{closeId}/variance-preview",
            new RequestDailyCloseVariancePreviewJson { Items = items },
            token);
    }
}
