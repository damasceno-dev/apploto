using System.Net;
using CommonTestUtilities.Requests;
using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerReopenHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.Admin)]
    public async Task Reopen_ShouldUnfreezeLedgerAndRequireFreshSubmitAndApprove(Role role)
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            $"DcReopenLifecycle{role}",
            role);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        var cashVarianceProduct = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var category = await factory.SeedCategoryAsync(branch.Id, defaultDirection: Direction.In);
        var transactionType = await factory.SeedTransactionTypeAsync(category.Id);
        var date = LocalToday();
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            DailyCloseStatus.Approved,
            submittedByOperatorId: op.Id,
            submittedAt: DateTime.UtcNow.AddMinutes(-20),
            approvedByUserId: user.Id,
            approvedAt: DateTime.UtcNow.AddMinutes(-10),
            rejectionReason: "legacy reason",
            notes: "keep this note");
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, 100m);
        var frozenVariance = await factory.SeedDailyCloseItemAsync(
            close.Id,
            cashVarianceProduct.Id,
            999m);

        var reopenResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/reopen", token);

        reopenResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reopened = await reopenResponse.ReadContentAsync<ResponseDailyCloseJson>();
        reopened.Status.ShouldBe(DailyCloseStatus.Draft);
        reopened.SubmittedAt.ShouldBeNull();
        reopened.ApprovedAt.ShouldBeNull();
        reopened.ApprovedByUserId.ShouldBeNull();
        reopened.RecordedByOperatorId.ShouldBe(op.Id);
        reopened.SubmittedByUserId.ShouldBeNull();
        reopened.SubmittedByOperatorId.ShouldBeNull();
        reopened.RejectionReason.ShouldBeNull();
        reopened.Notes.ShouldBe("keep this note");
        reopened.UpdatedAt.ShouldNotBeNull();
        reopened.UpdatedByUserId.ShouldBe(user.Id);
        reopened.Items.ShouldNotContain(item => item.ProductId == cashVarianceProduct.Id);
        reopened.ItemsFirstRecordedAt.ShouldNotBeNull();

        var persistedReopened = await factory.ReloadAsync<DailyClose>(close.Id);
        persistedReopened.ShouldNotBeNull();
        persistedReopened.Status.ShouldBe(DailyCloseStatus.Draft);
        persistedReopened.SubmittedAt.ShouldBeNull();
        persistedReopened.ApprovedAt.ShouldBeNull();
        persistedReopened.ApprovedByUserId.ShouldBeNull();
        persistedReopened.ItemsFirstRecordedAt.ShouldNotBeNull();
        var retainedVariance = await factory.ReloadAsync<DailyCloseItem>(frozenVariance.Id);
        retainedVariance.ShouldNotBeNull();
        retainedVariance.Value.ShouldBe(999m);
        retainedVariance.Active.ShouldBeTrue();

        var prematureApprove = await _client.PostAuthAsync($"/dailyclose/{close.Id}/approve", token);
        prematureApprove.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var approveError = await prematureApprove.ReadContentAsync<TestResponseErrorJson>();
        approveError.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_APPROVABLE);

        var transactionResponse = await _client.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJsonBuilder()
                .WithDate(date)
                .WithValue(25m)
                .WithAccountId(account.Id)
                .WithTransactionTypeId(transactionType.Id)
                .Build(),
            token);
        transactionResponse.StatusCode.ShouldBe(HttpStatusCode.Created);

        var submitResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);
        submitResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var submitted = await submitResponse.ReadContentAsync<ResponseDailyCloseJson>();
        submitted.Status.ShouldBe(DailyCloseStatus.Submitted);
        submitted.SubmittedAt.ShouldNotBeNull();
        submitted.ApprovedAt.ShouldBeNull();
        submitted.Items.Single(item => item.ProductId == cashVarianceProduct.Id).Value.ShouldBe(75m);

        var refreshedVariance = await factory.ReloadAsync<DailyCloseItem>(frozenVariance.Id);
        refreshedVariance.ShouldNotBeNull();
        refreshedVariance.Value.ShouldBe(75m);

        var approveResponse = await _client.PostAuthAsync($"/dailyclose/{close.Id}/approve", token);
        approveResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var approved = await approveResponse.ReadContentAsync<ResponseDailyCloseJson>();
        approved.Status.ShouldBe(DailyCloseStatus.Approved);
        approved.ApprovedAt.ShouldNotBeNull();
        approved.ApprovedByUserId.ShouldBe(user.Id);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
