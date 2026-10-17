using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

/// <summary>
/// M7.7 Phase 7.6.2: every refusal of the plan finalize leaves every installment unchanged, and no
/// per-row path can activate part of a plan.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TransactionControllerInstallmentPlanFinalizeUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PerRowFinalize_ShouldRefuseAnyDraftInstallment_OfADraftOrMixedPlan(bool mixed)
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, $"PlanBypass{mixed}", Role.Manager);
        var originId = mixed
            ? await ctx.SeedPlanAsync(TransactionStatus.Active, TransactionStatus.Draft)
            : await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft);
        var draft = (await GetPlan(originId, ctx.Token)).Installments.First(installment => installment.Status == TransactionStatus.Draft);

        var response = await _client.PostAuthAsync($"/transaction/{draft.Id}/finalize", ctx.Token, expectedVersion: draft.Version);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_FINALIZE_REQUIRED);
        (await factory.ReloadAsync<Transaction>(draft.Id))!.Status.ShouldBe(TransactionStatus.Draft);
    }

    [Fact]
    public async Task Finalize_ShouldRefuseAStaleVersion_LeavingEveryInstallmentDraft()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanStaleVersion", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);
        await TouchAsync(plan.Installments[2].Id);

        var response = await Finalize(originId, plan.Installments, ctx.Token);

        await ShouldConflict(response, ResourcesErrorMessages.TRANSACTION_PLAN_STALE_WRITE);
        await ShouldStayDraft(originId, ctx.Token, 3);
    }

    [Fact]
    public async Task Finalize_ShouldRefuseAnExpectedSetThatOmitsOrAddsInstallments()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanStaleSet", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);
        var standalone = await ctx.SeedStandaloneDraftAsync();

        var missing = await Finalize(originId, plan.Installments.Take(2), ctx.Token);
        var extra = await Finalize(
            originId,
            [.. plan.Installments, new ResponseTransactionJson { Id = standalone.Id, Version = standalone.Version }],
            ctx.Token);

        await ShouldConflict(missing, ResourcesErrorMessages.TRANSACTION_PLAN_STALE_WRITE);
        await ShouldConflict(extra, ResourcesErrorMessages.TRANSACTION_PLAN_STALE_WRITE);
        await ShouldStayDraft(originId, ctx.Token, 3);
    }

    [Fact]
    public async Task Finalize_ShouldRefuseAPlanWithNoDraftInstallment()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanNotDraft", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Active, TransactionStatus.Cancelled);
        var plan = await GetPlan(originId, ctx.Token);
        plan.Status.ShouldBe(InstallmentPlanStatus.Active);

        var response = await Finalize(originId, plan.Installments, ctx.Token);

        await ShouldConflict(response, ResourcesErrorMessages.TRANSACTION_PLAN_NOT_DRAFT);
    }

    [Fact]
    public async Task Finalize_ShouldRefuseALockedDate_LeavingEveryInstallmentDraft()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanLocked", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);
        await factory.SeedSettingAsync(ctx.BranchId, lockDate: ctx.Today);

        var response = await Finalize(originId, plan.Installments, ctx.Token);

        await ShouldConflict(response, ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);
        await ShouldStayDraft(originId, ctx.Token, 2);
    }

    [Fact]
    public async Task Finalize_ShouldRefuseAFrozenTerminalLedger_LeavingEveryInstallmentDraft()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanFrozen", Role.Manager);
        var terminal = await factory.SeedAccountAsync(ctx.BranchId, AccountType.Terminal);
        var originId = await ctx.SeedPlanOnAsync(terminal.Id, ctx.Today, TransactionStatus.Draft, TransactionStatus.Draft);
        await factory.SeedDailyCloseAsync(ctx.BranchId, terminal.Id, ctx.Today, DailyCloseStatus.Submitted);
        var plan = await GetPlan(originId, ctx.Token);

        var response = await Finalize(originId, plan.Installments, ctx.Token);

        await ShouldConflict(response, ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
        await ShouldStayDraft(originId, ctx.Token, 2);
    }

    [Fact]
    public async Task Finalize_ShouldRefuseAMemberOutsideTheAccountScope()
    {
        var owner = await InstallmentPlanTestContext.SeedAsync(factory, "PlanMemberScope", Role.Manager);
        var originId = await owner.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, owner.Token);
        var memberUser = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(memberUser.Id, owner.BranchId, Role.Member);
        await factory.SeedOperatorAsync(owner.BranchId, userId: memberUser.Id);
        var memberToken = factory.IssueBranchToken(membership);

        var finalize = await Finalize(originId, plan.Installments, memberToken);
        var read = await _client.GetAuthAsync($"/transaction/installment/{originId}", memberToken);

        finalize.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await finalize.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        read.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await ShouldStayDraft(originId, owner.Token, 2);
    }

    [Fact]
    public async Task Finalize_ShouldRefuseAMemberWhoIsNotTheRecordingOperator()
    {
        var owner = await InstallmentPlanTestContext.SeedAsync(factory, "PlanMemberOwnership", Role.Manager);
        var originId = await owner.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, owner.Token);
        var memberUser = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(memberUser.Id, owner.BranchId, Role.Member);
        var memberOperator = await factory.SeedOperatorAsync(owner.BranchId, userId: memberUser.Id);
        await factory.SeedOperatorAccountAsync(memberOperator.Id, owner.BankAccountId);

        var finalize = await Finalize(originId, plan.Installments, factory.IssueBranchToken(membership));

        finalize.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await finalize.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
        await ShouldStayDraft(originId, owner.Token, 2);
    }

    [Fact]
    public async Task Finalize_ShouldReturn400_ForAMalformedExpectedSet()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanValidation", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);
        var first = plan.Installments[0];

        var empty = await Finalize(originId, [], ctx.Token);
        var duplicate = await Finalize(originId, [first, first], ctx.Token);
        var zeroVersion = await Finalize(originId, [new ResponseTransactionJson { Id = first.Id, Version = 0 }], ctx.Token);

        await ShouldBadRequest(empty, ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENTS_REQUIRED);
        await ShouldBadRequest(duplicate, ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENT_ID_INVALID);
        await ShouldBadRequest(zeroVersion, ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENT_VERSION_INVALID);
        await ShouldStayDraft(originId, ctx.Token, 2);
    }

    [Fact]
    public async Task Finalize_ShouldReturn400_ForANullInstallmentEntry()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanNullEntry", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft);

        var response = await _client.PostAuthAsync(
            $"/transaction/installment/{originId}/finalize",
            new { installments = new object?[] { null } },
            ctx.Token);

        await ShouldBadRequest(response, ResourcesErrorMessages.TRANSACTION_PLAN_INSTALLMENT_ID_INVALID);
        await ShouldStayDraft(originId, ctx.Token, 2);
    }

    [Fact]
    public async Task PlanReadAndFinalize_ShouldReturn404_ForAnUnknownStandaloneOrForeignPlan()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanNotFound", Role.Manager);
        var standalone = await ctx.SeedStandaloneDraftAsync();
        var foreign = await InstallmentPlanTestContext.SeedAsync(factory, "PlanNotFoundForeign", Role.Manager);
        var foreignOrigin = await foreign.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft);

        foreach (var id in new[] { Guid.NewGuid(), standalone.Id, foreignOrigin })
        {
            var read = await _client.GetAuthAsync($"/transaction/installment/{id}", ctx.Token);
            var finalize = await Finalize(id, [new ResponseTransactionJson { Id = id, Version = 1 }], ctx.Token);

            read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
            (await read.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
                .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_NOT_FOUND);
            finalize.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        }

        await ShouldStayDraft(foreignOrigin, foreign.Token, 2);
    }

    private async Task<ResponseInstallmentPlanJson> GetPlan(Guid originId, string token)
    {
        var response = await _client.GetAuthAsync($"/transaction/installment/{originId}", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseInstallmentPlanJson>();
    }

    private Task<HttpResponseMessage> Finalize(Guid originId, IEnumerable<ResponseTransactionJson> installments, string token)
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
                ]
            },
            token);
    }

    private async Task TouchAsync(Guid transactionId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""UPDATE "Transactions" SET "Description" = 'Editado por outra sessão' WHERE "Id" = {transactionId}""");
    }

    private async Task ShouldStayDraft(Guid originId, string token, int expectedDrafts)
    {
        var plan = await GetPlan(originId, token);
        plan.DraftCount.ShouldBe(expectedDrafts);
        plan.ActiveCount.ShouldBe(0);
    }

    private static async Task ShouldConflict(HttpResponseMessage response, string expectedKey)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(expectedKey);
    }

    private static async Task ShouldBadRequest(HttpResponseMessage response, string expectedKey)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(expectedKey);
    }
}
