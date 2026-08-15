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
public class DailyCloseControllerOpeningRecheckOwnershipTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ElevatedOpen_MemberClaim_ManagerSubmit_ShouldPreserveRecorderThroughOpeningRecheck()
    {
        var (opener, branch, _, openerToken) = await factory.SeedFullBranchContextAsync(
            "DcFirstClaimJourney",
            Role.Admin);
        var managerUser = await factory.SeedUserAsync();
        var managerMembership = await factory.SeedBranchUserAsync(managerUser.Id, branch.Id, Role.Manager);
        var managerToken = factory.IssueBranchToken(managerMembership);
        var managerOperator = await factory.SeedOperatorAsync(branch.Id, userId: managerUser.Id);
        var recordingUser = await factory.SeedUserAsync();
        var recordingMembership = await factory.SeedBranchUserAsync(recordingUser.Id, branch.Id, Role.Member);
        var recordingToken = factory.IssueBranchToken(recordingMembership);
        var recordingOperator = await factory.SeedOperatorAsync(branch.Id, userId: recordingUser.Id);
        var otherScopedUser = await factory.SeedUserAsync();
        var otherScopedMembership = await factory.SeedBranchUserAsync(otherScopedUser.Id, branch.Id, Role.Member);
        var otherScopedToken = factory.IssueBranchToken(otherScopedMembership);
        var otherScopedOperator = await factory.SeedOperatorAsync(branch.Id, userId: otherScopedUser.Id);
        var unscopedUser = await factory.SeedUserAsync();
        var unscopedMembership = await factory.SeedBranchUserAsync(unscopedUser.Id, branch.Id, Role.Member);
        var unscopedToken = factory.IssueBranchToken(unscopedMembership);
        await factory.SeedOperatorAsync(branch.Id, userId: unscopedUser.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(recordingOperator.Id, account.Id);
        await factory.SeedOperatorAccountAsync(otherScopedOperator.Id, account.Id);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        var cashVariance = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var predecessorDate = LocalToday().AddDays(-4);
        var closeDate = predecessorDate.AddDays(1);
        var predecessor = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            predecessorDate,
            DailyCloseStatus.Draft,
            openedByUserId: managerUser.Id,
            recordedByUserId: managerUser.Id,
            recordedByOperatorId: managerOperator.Id);
        await factory.SeedDailyCloseItemAsync(predecessor.Id, product.Id, 100m);

        var openedResponse = await _client.PostAuthAsync(
            "/dailyclose",
            new RequestOpenDailyCloseJson { AccountId = account.Id, Date = closeDate },
            openerToken);

        openedResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var opened = await openedResponse.ReadContentAsync<ResponseDailyCloseJson>();
        opened.OpenedByUserId.ShouldBe(opener.Id);
        opened.RecordedByUserId.ShouldBeNull();
        opened.RecordedByOperatorId.ShouldBeNull();
        opened.SubmittedByUserId.ShouldBeNull();
        opened.SubmittedByOperatorId.ShouldBeNull();

        var firstCount = await PutAsync(opened.Id, opened.Version, product.Id, 80m, recordingToken);

        firstCount.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await firstCount.Content.ReadAsStringAsync());
        var claimed = await firstCount.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        claimed.DailyClose.RecordedByUserId.ShouldBe(recordingUser.Id);
        claimed.DailyClose.RecordedByOperatorId.ShouldBe(recordingOperator.Id);
        claimed.DailyClose.SubmittedByUserId.ShouldBeNull();

        var memberFreshBackdatedSubmit = await _client.PostAuthAsync(
            $"/dailyclose/{opened.Id}/submit",
            recordingToken);
        memberFreshBackdatedSubmit.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var freshError = await memberFreshBackdatedSubmit.ReadContentAsync<TestResponseErrorJson>();
        freshError.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);

        var managerSubmit = await _client.PostAuthAsync(
            $"/dailyclose/{opened.Id}/submit",
            managerToken);

        managerSubmit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var submitted = await managerSubmit.ReadContentAsync<ResponseDailyCloseJson>();
        submitted.RecordedByUserId.ShouldBe(recordingUser.Id);
        submitted.RecordedByOperatorId.ShouldBe(recordingOperator.Id);
        submitted.SubmittedByUserId.ShouldBe(managerUser.Id);
        submitted.SubmittedByOperatorId.ShouldBe(managerOperator.Id);
        var submittedState = await factory.ReloadAsync<DailyClose>(opened.Id);
        submittedState.ShouldNotBeNull();
        submittedState.RecordedByUserId.ShouldBe(recordingUser.Id);
        submittedState.RecordedByOperatorId.ShouldBe(recordingOperator.Id);
        submittedState.SubmittedByUserId.ShouldBe(managerUser.Id);
        submittedState.SubmittedByOperatorId.ShouldBe(managerOperator.Id);

        var getResponse = await _client.GetAuthAsync($"/dailyclose/{opened.Id}", managerToken);
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var get = await getResponse.ReadContentAsync<ResponseDailyCloseJson>();
        get.RecordedByOperatorName.ShouldBe(recordingOperator.Name);
        get.SubmittedByOperatorName.ShouldBe(managerOperator.Name);
        var reviewResponse = await _client.GetAuthAsync($"/dailyclose/{opened.Id}/review", managerToken);
        reviewResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var review = await reviewResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        review.RecordedByOperatorId.ShouldBe(recordingOperator.Id);
        review.SubmittedByOperatorId.ShouldBe(managerOperator.Id);
        var listResponse = await _client.GetAuthAsync(
            $"/dailyclose?operatorId={recordingOperator.Id}",
            managerToken);
        listResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var list = await listResponse.ReadContentAsync<ResponseListDailyClosesJson>();
        list.Items.ShouldContain(item =>
            item.Id == opened.Id &&
            item.RecordedByOperatorId == recordingOperator.Id &&
            item.SubmittedByOperatorId == managerOperator.Id);
        var dashboardResponse = await _client.GetAuthAsync(
            $"/report/dashboard?date={closeDate:yyyy-MM-dd}",
            managerToken);
        dashboardResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dashboard = await dashboardResponse.ReadContentAsync<ResponseDashboardJson>();
        var dashboardClose = dashboard.Closes.Single(close => close.DailyCloseId == opened.Id);
        dashboardClose.RecordedByOperatorId.ShouldBe(recordingOperator.Id);
        dashboardClose.SubmittedByOperatorId.ShouldBe(managerOperator.Id);

        var predecessorEdit = await PutAsync(
            predecessor.Id,
            predecessor.Version,
            product.Id,
            120m,
            managerToken);
        predecessorEdit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cascade = await predecessorEdit.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        cascade.AffectedSuccessor.ShouldNotBeNull();
        cascade.AffectedSuccessor.DailyCloseId.ShouldBe(opened.Id);
        var demoted = await factory.ReloadAsync<DailyClose>(opened.Id);
        demoted.ShouldNotBeNull();
        demoted.Status.ShouldBe(DailyCloseStatus.Draft);
        demoted.RecordedByUserId.ShouldBe(recordingUser.Id);
        demoted.RecordedByOperatorId.ShouldBe(recordingOperator.Id);
        demoted.SubmittedByUserId.ShouldBeNull();
        demoted.SubmittedByOperatorId.ShouldBeNull();

        var otherScopedAttempt = await PutAsync(
            opened.Id,
            demoted.Version,
            product.Id,
            85m,
            otherScopedToken);
        otherScopedAttempt.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await otherScopedAttempt.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
        var unscopedAttempt = await PutAsync(
            opened.Id,
            demoted.Version,
            product.Id,
            85m,
            unscopedToken);
        unscopedAttempt.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await unscopedAttempt.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);

        var correction = await PutAsync(
            opened.Id,
            demoted.Version,
            product.Id,
            85m,
            recordingToken);
        correction.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corrected = await correction.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        corrected.DailyClose.RecordedByUserId.ShouldBe(recordingUser.Id);
        corrected.DailyClose.RecordedByOperatorId.ShouldBe(recordingOperator.Id);
        var resubmit = await _client.PostAuthAsync($"/dailyclose/{opened.Id}/submit", recordingToken);
        resubmit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resubmitted = await resubmit.ReadContentAsync<ResponseDailyCloseJson>();
        resubmitted.RecordedByUserId.ShouldBe(recordingUser.Id);
        resubmitted.RecordedByOperatorId.ShouldBe(recordingOperator.Id);
        resubmitted.SubmittedByUserId.ShouldBe(recordingUser.Id);
        resubmitted.SubmittedByOperatorId.ShouldBe(recordingOperator.Id);
        resubmitted.Items.Single(item => item.ProductId == cashVariance.Id).Value.ShouldBe(-35m);
    }

    [Fact]
    public async Task ElevatedRecorderWithoutOperator_ShouldRemainReachableAfterOpeningRecheck()
    {
        var (manager, branch, _, managerToken) = await factory.SeedFullBranchContextAsync(
            "DcElevatedRecorderNoOperator",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var predecessorDate = LocalToday().AddDays(-5);
        var predecessor = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            predecessorDate,
            DailyCloseStatus.Draft,
            openedByUserId: manager.Id,
            recordedByUserId: manager.Id,
            recordedByOperatorId: null);
        await factory.SeedDailyCloseItemAsync(predecessor.Id, product.Id, 50m);
        var openResponse = await _client.PostAuthAsync(
            "/dailyclose",
            new RequestOpenDailyCloseJson
            {
                AccountId = account.Id,
                Date = predecessorDate.AddDays(1)
            },
            managerToken);
        openResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var opened = await openResponse.ReadContentAsync<ResponseDailyCloseJson>();

        var firstCount = await PutAsync(opened.Id, opened.Version, product.Id, 40m, managerToken);
        firstCount.StatusCode.ShouldBe(
            HttpStatusCode.OK,
            await firstCount.Content.ReadAsStringAsync());
        var counted = await firstCount.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        counted.DailyClose.RecordedByUserId.ShouldBe(manager.Id);
        counted.DailyClose.RecordedByOperatorId.ShouldBeNull();
        var submit = await _client.PostAuthAsync($"/dailyclose/{opened.Id}/submit", managerToken);
        submit.StatusCode.ShouldBe(HttpStatusCode.OK);

        var predecessorEdit = await PutAsync(
            predecessor.Id,
            predecessor.Version,
            product.Id,
            60m,
            managerToken);
        predecessorEdit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var demoted = await factory.ReloadAsync<DailyClose>(opened.Id);
        demoted.ShouldNotBeNull();
        demoted.Status.ShouldBe(DailyCloseStatus.Draft);
        demoted.RecordedByUserId.ShouldBe(manager.Id);
        demoted.RecordedByOperatorId.ShouldBeNull();

        var correction = await PutAsync(opened.Id, demoted.Version, product.Id, 45m, managerToken);
        correction.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corrected = await correction.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        corrected.DailyClose.RecordedByUserId.ShouldBe(manager.Id);
        corrected.DailyClose.RecordedByOperatorId.ShouldBeNull();
    }

    [Fact]
    public async Task OpeningRecheck_ShouldAllowRecordingMemberCorrectionAndResubmit_ButDenyOtherMember()
    {
        var (manager, branch, _, managerToken) = await factory.SeedFullBranchContextAsync(
            "DcRecheckOwnership",
            Role.Manager);
        var managerOperator = await factory.SeedOperatorAsync(branch.Id, userId: manager.Id);
        var recordingUser = await factory.SeedUserAsync();
        var recordingMembership = await factory.SeedBranchUserAsync(recordingUser.Id, branch.Id, Role.Member);
        var recordingToken = factory.IssueBranchToken(recordingMembership);
        var recordingOperator = await factory.SeedOperatorAsync(branch.Id, userId: recordingUser.Id);
        var otherUser = await factory.SeedUserAsync();
        var otherMembership = await factory.SeedBranchUserAsync(otherUser.Id, branch.Id, Role.Member);
        var otherToken = factory.IssueBranchToken(otherMembership);
        var otherOperator = await factory.SeedOperatorAsync(branch.Id, userId: otherUser.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(recordingOperator.Id, account.Id);
        await factory.SeedOperatorAccountAsync(otherOperator.Id, account.Id);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 10);
        var cashVariance = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName,
            displayOrder: 20);
        var date = LocalToday().AddDays(-3);
        var predecessor = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            DailyCloseStatus.Draft,
            submittedByOperatorId: managerOperator.Id);
        await factory.SeedDailyCloseItemAsync(predecessor.Id, product.Id, 100m);
        var successor = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date.AddDays(1),
            DailyCloseStatus.Submitted,
            submittedByOperatorId: recordingOperator.Id,
            submittedAt: DateTime.UtcNow.AddDays(-2));
        await factory.SeedDailyCloseItemAsync(successor.Id, product.Id, 80m);
        var retainedVariance = await factory.SeedDailyCloseItemAsync(successor.Id, cashVariance.Id, -20m);

        var predecessorEdit = await PutAsync(
            predecessor.Id,
            predecessor.Version,
            product.Id,
            120m,
            managerToken);
        predecessorEdit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var cascade = await predecessorEdit.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        cascade.AffectedSuccessor.ShouldNotBeNull();
        cascade.AffectedSuccessor.DailyCloseId.ShouldBe(successor.Id);

        var demoted = await factory.ReloadAsync<DailyClose>(successor.Id);
        demoted.ShouldNotBeNull();
        demoted.OpeningRecheckRequiredAt.ShouldNotBeNull();
        demoted.RecordedByUserId.ShouldBe(recordingUser.Id);
        demoted.RecordedByOperatorId.ShouldBe(recordingOperator.Id);

        var otherAttempt = await PutAsync(
            successor.Id,
            demoted.Version,
            product.Id,
            85m,
            otherToken);
        otherAttempt.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var otherError = await otherAttempt.ReadContentAsync<TestResponseErrorJson>();
        otherError.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);

        var correction = await PutAsync(
            successor.Id,
            demoted.Version,
            product.Id,
            85m,
            recordingToken);
        correction.StatusCode.ShouldBe(HttpStatusCode.OK);
        var corrected = await correction.ReadContentAsync<ResponsePutDailyCloseItemsJson>();
        corrected.DailyClose.OpeningRecheckRequiredAt.ShouldNotBeNull();
        corrected.DailyClose.Items.ShouldNotContain(item => item.ProductId == cashVariance.Id);

        var resubmit = await _client.PostAuthAsync(
            $"/dailyclose/{successor.Id}/submit",
            recordingToken);
        resubmit.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resubmitted = await resubmit.ReadContentAsync<ResponseDailyCloseJson>();
        resubmitted.Status.ShouldBe(DailyCloseStatus.Submitted);
        resubmitted.OpeningRecheckRequiredAt.ShouldBeNull();
        resubmitted.OpeningRecheckTriggeredByDailyCloseId.ShouldBeNull();
        resubmitted.OpeningRecheckTriggeredByUserId.ShouldBeNull();
        resubmitted.Items.Single(item => item.ProductId == cashVariance.Id).Value.ShouldBe(-35m);
        var persistedVariance = await factory.ReloadAsync<DailyCloseItem>(retainedVariance.Id);
        persistedVariance.ShouldNotBeNull();
        persistedVariance.Value.ShouldBe(-35m);
    }

    [Theory]
    [InlineData(ManagerRelease.Recall)]
    [InlineData(ManagerRelease.Reopen)]
    public async Task DirectPriorDayElevatedRelease_ShouldRemainManagerOwned(ManagerRelease release)
    {
        var (_, branch, _, managerToken) = await factory.SeedFullBranchContextAsync(
            $"DcManagerOwned{release}",
            Role.Manager);
        var memberUser = await factory.SeedUserAsync();
        var memberMembership = await factory.SeedBranchUserAsync(memberUser.Id, branch.Id, Role.Member);
        var memberToken = factory.IssueBranchToken(memberMembership);
        var memberOperator = await factory.SeedOperatorAsync(branch.Id, userId: memberUser.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(memberOperator.Id, account.Id);
        var product = await factory.SeedProductAsync(branch.Id);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
        var status = release == ManagerRelease.Recall
            ? DailyCloseStatus.Submitted
            : DailyCloseStatus.Approved;
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            LocalToday().AddDays(-3),
            status,
            submittedByOperatorId: memberOperator.Id,
            submittedAt: DateTime.UtcNow.AddDays(-3),
            approvedAt: status == DailyCloseStatus.Approved ? DateTime.UtcNow.AddDays(-2) : null);
        await factory.SeedDailyCloseItemAsync(close.Id, product.Id, 10m);

        var released = await _client.PostAuthAsync(
            $"/dailyclose/{close.Id}/{release.ToString().ToLowerInvariant()}",
            managerToken);
        released.StatusCode.ShouldBe(HttpStatusCode.OK);
        var releasedPayload = await released.ReadContentAsync<ResponseDailyCloseJson>();
        releasedPayload.OpeningRecheckRequiredAt.ShouldBeNull();
        releasedPayload.RejectionReason.ShouldBeNull();

        var memberAttempt = await PutAsync(
            close.Id,
            releasedPayload.Version,
            product.Id,
            11m,
            memberToken);
        memberAttempt.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await memberAttempt.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
    }

    private Task<HttpResponseMessage> PutAsync(
        Guid closeId,
        uint version,
        Guid productId,
        decimal value,
        string token)
    {
        return _client.PutAuthAsync(
            $"/dailyclose/{closeId}/items",
            new RequestPutDailyCloseItemsJson
            {
                Version = version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = productId, Value = value }]
            },
            token);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }

    public enum ManagerRelease
    {
        Recall,
        Reopen
    }
}
