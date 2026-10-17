using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

/// <summary>
/// M7.7 Phase 7.6.2 (gap Y): a wholly Draft cheque plan activates all-or-nothing through
/// <c>POST /transaction/installment/{originTransactionId}/finalize</c>; a Mixed plan finalizes its
/// remaining Draft installments only with explicit confirmation.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TransactionControllerInstallmentPlanFinalizeHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Finalize_ShouldActivateEveryInstallmentAtOnce_ForADraftPlanCreatedThroughTheApi()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanFinalizeApi", Role.Manager);
        var create = await _client.PostAuthAsync(
            "/transaction/installment",
            new RequestCreateTransactionInstallmentJsonBuilder()
                .WithDate(ctx.Today)
                .WithInstallments(
                [
                    new RequestCreateTransactionInstallmentItemJson { DueDate = ctx.Today.AddDays(30), Value = 100m },
                    new RequestCreateTransactionInstallmentItemJson { DueDate = ctx.Today.AddDays(60), Value = 100m },
                    new RequestCreateTransactionInstallmentItemJson { DueDate = ctx.Today.AddDays(90), Value = 100m }
                ])
                .WithTransactionTypeId(ctx.ChequeTypeId)
                .WithAccountId(ctx.BankAccountId)
                .WithRecordedByOperatorId(ctx.OperatorId)
                .WithSaveAsDraft(true)
                .Build(),
            ctx.Token);
        create.StatusCode.ShouldBe(HttpStatusCode.Created, await create.Content.ReadAsStringAsync());
        var originId = (await create.ReadContentAsync<ResponseCreateTransactionInstallmentJson>()).Installments[0].Id;

        var before = await GetPlan(originId, ctx.Token);
        before.Status.ShouldBe(InstallmentPlanStatus.Draft);
        before.DraftCount.ShouldBe(3);
        before.TotalValue.ShouldBe(300m);
        (await SumActiveAsync(ctx.BranchId, ctx.BankAccountId)).ShouldBe(0m);

        var finalize = await Finalize(originId, before.Installments, ctx.Token);

        finalize.StatusCode.ShouldBe(HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        var after = await finalize.ReadContentAsync<ResponseInstallmentPlanJson>();
        after.Status.ShouldBe(InstallmentPlanStatus.Active);
        after.ActiveCount.ShouldBe(3);
        after.DraftCount.ShouldBe(0);
        after.Installments.ShouldAllBe(installment => installment.Status == TransactionStatus.Active);
        after.Installments.Select(installment => installment.Version)
            .ShouldAllBe(version => version != before.Installments[0].Version);
        // The whole R$ 300 becomes official in one step; no R$ 100/R$ 200 state was ever visible.
        (await SumActiveAsync(ctx.BranchId, ctx.BankAccountId)).ShouldBe(300m);
        (await GetPlan(originId, ctx.Token)).Status.ShouldBe(InstallmentPlanStatus.Active);

        var aging = await _client.GetAuthAsync("/report/cheques/open-aging?page=1&pageSize=10", ctx.Token);
        aging.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await aging.ReadContentAsync<ResponseOpenChequeAgingJson>()).Items
            .ShouldContain(group => group.OriginTransactionId == originId && group.OutstandingTotal == 300m);
    }

    [Fact]
    public async Task Finalize_ShouldActivateOnlyTheRemainingDrafts_WhenAMixedPlanIsExplicitlyConfirmed()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanFinalizeMixed", Role.Manager);
        var originId = await ctx.SeedPlanAsync(
            TransactionStatus.Draft, TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);
        var cancelledId = plan.Installments[1].Id;
        var cancel = await _client.PostAuthAsync(
            $"/transaction/{cancelledId}/cancel",
            new RequestCancelTransactionJson { CancellationReason = "Cheque devolvido pelo cliente" },
            ctx.Token,
            expectedVersion: plan.Installments[1].Version);
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());

        var mixed = await GetPlan(originId, ctx.Token);
        mixed.Status.ShouldBe(InstallmentPlanStatus.Mixed);
        mixed.TotalValue.ShouldBe(200m);
        var remaining = mixed.Installments.Where(installment => installment.Status == TransactionStatus.Draft).ToList();

        var unconfirmed = await Finalize(originId, remaining, ctx.Token);
        unconfirmed.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await unconfirmed.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(server.Exceptions.ResourcesErrorMessages.TRANSACTION_PLAN_MIXED_CONFIRMATION_REQUIRED);
        (await GetPlan(originId, ctx.Token)).DraftCount.ShouldBe(2);

        var confirmed = await Finalize(originId, remaining, ctx.Token, confirmMixedPlan: true);

        confirmed.StatusCode.ShouldBe(HttpStatusCode.OK, await confirmed.Content.ReadAsStringAsync());
        var after = await confirmed.ReadContentAsync<ResponseInstallmentPlanJson>();
        after.Status.ShouldBe(InstallmentPlanStatus.Active);
        after.ActiveCount.ShouldBe(2);
        after.CancelledCount.ShouldBe(1);
        after.Installments.Single(installment => installment.Id == cancelledId).Status.ShouldBe(TransactionStatus.Cancelled);
        (await SumActiveAsync(ctx.BranchId, ctx.BankAccountId)).ShouldBe(200m);
    }

    [Fact]
    public async Task Finalize_ShouldLetTheRecordingMemberFinalizeTheirSameDayPlan()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanFinalizeMember", Role.Member);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);

        var finalize = await Finalize(originId, plan.Installments, ctx.Token);

        finalize.StatusCode.ShouldBe(HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        (await finalize.ReadContentAsync<ResponseInstallmentPlanJson>()).Status.ShouldBe(InstallmentPlanStatus.Active);
    }

    [Fact]
    public async Task PerRowFinalize_ShouldStillFinalizeAStandaloneDraft()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanStandaloneDraft", Role.Manager);
        var standalone = await ctx.SeedStandaloneDraftAsync();

        var finalize = await _client.PostAuthAsync(
            $"/transaction/{standalone.Id}/finalize",
            ctx.Token,
            expectedVersion: standalone.Version);

        finalize.StatusCode.ShouldBe(HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        (await finalize.ReadContentAsync<ResponseTransactionJson>()).Status.ShouldBe(TransactionStatus.Active);
    }

    private async Task<ResponseInstallmentPlanJson> GetPlan(Guid originId, string token)
    {
        var response = await _client.GetAuthAsync($"/transaction/installment/{originId}", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseInstallmentPlanJson>();
    }

    private Task<HttpResponseMessage> Finalize(
        Guid originId,
        IEnumerable<ResponseTransactionJson> installments,
        string token,
        bool confirmMixedPlan = false)
    {
        return _client.PostAuthAsync(
            $"/transaction/installment/{originId}/finalize",
            new RequestFinalizeInstallmentPlanJson
            {
                Installments =
                [
                    .. installments.Select(installment => new RequestInstallmentPlanRowVersionJson
                    {
                        TransactionId = installment.Id,
                        Version = installment.Version
                    })
                ],
                ConfirmMixedPlan = confirmMixedPlan
            },
            token);
    }

    private async Task<decimal> SumActiveAsync(Guid branchId, Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.Transactions
            .Where(transaction =>
                transaction.BranchId == branchId &&
                transaction.AccountId == accountId &&
                transaction.Active &&
                transaction.Status == TransactionStatus.Active)
            .SumAsync(transaction => transaction.Value);
    }
}
