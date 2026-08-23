using System.Net;
using System.Net.Http.Json;
using server.Application.Services.DailyCloses;
using server.Application.UseCases.DailyCloses;
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
public class DailyCloseControllerPutItemsUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // ──────────────────────────────────────────────
    // 401 — no token
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new VersionedRequestPutDailyCloseItemsJson
        {
            Version = 1,
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = Guid.NewGuid(), Value = 10m }]
        };

        var httpResponse = await _client.PutAsync(
            $"/dailyclose/{Guid.NewGuid()}/items",
            JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    // ──────────────────────────────────────────────
    // 409 — Approved close is never editable
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn409_WhenCloseIsApproved()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutApproved", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, status: DailyCloseStatus.Approved);
        var product = await factory.SeedProductAsync(branch.Id);

        var request = new VersionedRequestPutDailyCloseItemsJson
        {
            Version = close.Version,
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 10m }]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
    }

    // ──────────────────────────────────────────────
    // 409 — Submitted is unconditionally non-editable through ordinary item save
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn409_WhenSubmittedAndMemberIsNotRecordingOperator()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutOtherOp", Role.Member);

        var callerOp = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var otherOp = await factory.SeedOperatorAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(callerOp.Id, account.Id);

        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            status: DailyCloseStatus.Submitted,
            submittedByOperatorId: otherOp.Id,   // submitted by someone else
            submittedAt: DateTime.UtcNow.AddHours(-1));

        var request = new VersionedRequestPutDailyCloseItemsJson
        {
            Version = close.Version,
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = Guid.NewGuid(), Value = 10m }]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
    }

    // ──────────────────────────────────────────────
    // 409 — ordinary item save never doubles as Recall
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn409_WhenSubmittedEvenForRecordingMemberOnOlderDay()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutOlderDay", Role.Member);

        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);

        var spTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var olderDay = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, spTimeZone).Date.AddDays(-2);

        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: olderDay,
            status: DailyCloseStatus.Submitted,
            submittedByOperatorId: op.Id,          // same operator
            submittedAt: DateTime.UtcNow.AddDays(-2));

        var request = new VersionedRequestPutDailyCloseItemsJson
        {
            Version = close.Version,
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = Guid.NewGuid(), Value = 10m }]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
    }

    [Fact]
    public async Task PutItems_ShouldReturn409_WhenSubmittedEvenForRecordingMemberOnSameDay()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcPutSubmittedSameDayMember",
            Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date: LocalToday(),
            status: DailyCloseStatus.Submitted,
            submittedByOperatorId: op.Id,
            submittedAt: DateTime.UtcNow.AddMinutes(-10));

        var response = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = close.Version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = Guid.NewGuid(), Value = 10m }]
            },
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_EDITABLE);
        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Submitted);
        persisted.SubmittedAt.ShouldNotBeNull();
    }

    // ──────────────────────────────────────────────
    // 403 — Member not linked to the close's account
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn403_WhenMemberIsOutOfScope()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutOutOfScope", Role.Member);

        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        // Operator has no OperatorAccount for the close's account — scope check will fail.
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, status: DailyCloseStatus.Draft);

        var request = new VersionedRequestPutDailyCloseItemsJson
        {
            Version = close.Version,
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = Guid.NewGuid(), Value = 10m }]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    // ──────────────────────────────────────────────
    // 404 — cross-branch daily close
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn404_WhenDailyCloseBelongsToDifferentBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutXBranch", Role.Manager);
        var otherBranch = await factory.SeedBranchForOtherContextAsync("DcPutXBranchOther");
        var otherAccount = await factory.SeedAccountAsync(otherBranch.Id, AccountType.Terminal);
        var otherClose = await factory.SeedDailyCloseAsync(otherBranch.Id, otherAccount.Id);

        var request = new VersionedRequestPutDailyCloseItemsJson
        {
            Version = otherClose.Version,
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = Guid.NewGuid(), Value = 10m }]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{otherClose.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);
    }

    // ──────────────────────────────────────────────
    // 400 — payload references the CashVariance product
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn400_WhenPayloadReferencesCashVarianceProduct()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutCvForbidden", Role.Manager);

        // The resolver must be able to find the CashVariance product by name.
        var cvProduct = await factory.SeedProductAsync(
            branch.Id, name: CashVarianceProductResolver.CashVarianceProductName);

        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id, status: DailyCloseStatus.Draft);

        // Payload directly references the CashVariance product — must be rejected.
        var request = new VersionedRequestPutDailyCloseItemsJson
        {
            Version = close.Version,
            Items = [new RequestUpsertDailyCloseItemJson { ProductId = cvProduct.Id, Value = 10m }]
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEM_PRODUCT_FORBIDDEN);
    }

    // ──────────────────────────────────────────────
    // 400 — Items list is null (FluentValidation)
    // ──────────────────────────────────────────────

    [Fact]
    public async Task PutItems_ShouldReturn400_WhenItemsIsNull()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("DcPutNullItems", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);

        var request = new VersionedRequestPutDailyCloseItemsJson
        {
            Version = close.Version,
            Items = null
        };

        var httpResponse = await _client.PutAuthAsync($"/dailyclose/{close.Id}/items", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_ITEMS_REQUIRED);
    }

    [Fact]
    public async Task PutItems_ShouldReturn409AndPreserveFirstWrite_WhenVersionIsStale()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcPutStaleVersion",
            Role.Manager);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);

        var firstResponse = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = close.Version,
                Notes = "first write",
                Items =
                [
                    new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 10m }
                ]
            },
            token);
        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPayload = (await firstResponse.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;

        var staleResponse = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = close.Version,
                Notes = "stale overwrite",
                Items =
                [
                    new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 99m }
                ]
            },
            token);

        staleResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await staleResponse.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_STALE_WRITE);
        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Version.ShouldBe(firstPayload.Version);
        persisted.Notes.ShouldBe("first write");
        var items = await factory.ListDailyCloseItemsByDailyCloseIdAsync(close.Id);
        items.ShouldHaveSingleItem().Value.ShouldBe(10m);
    }

    [Fact]
    public async Task PutItems_ShouldReturn400WithVerbatimError_WhenNotesExceedMaxLength()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcPutNotesTooLong",
            Role.Admin);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);

        var response = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = close.Version,
                Items = [],
                Notes = new string('x', DailyCloseValidationExtensions.NotesMaxLength + 1)
            },
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(string.Format(
            ResourcesErrorMessages.DAILYCLOSE_NOTES_LENGTH,
            DailyCloseValidationExtensions.NotesMaxLength));
    }

    [Fact]
    public async Task PutItems_ShouldReturn409_WhenRejectedCorrectionDateIsLocked()
    {
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(
            "DcPutRejectedLocked",
            Role.Member);
        var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        await factory.SeedOperatorAccountAsync(op.Id, account.Id);
        await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName);
        var product = await factory.SeedProductAsync(branch.Id);
        var date = LocalToday().AddDays(-1);
        var close = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            date,
            DailyCloseStatus.Rejected,
            submittedByOperatorId: op.Id,
            rejectionReason: "corrigir");
        await factory.SeedSettingAsync(branch.Id, lockDate: date);

        var response = await _client.PutAuthAsync(
            $"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = close.Version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = product.Id, Value = 10m }]
            },
            token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Status.ShouldBe(DailyCloseStatus.Rejected);
    }

    private static DateTime LocalToday()
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, timeZone).Date;
    }
}
