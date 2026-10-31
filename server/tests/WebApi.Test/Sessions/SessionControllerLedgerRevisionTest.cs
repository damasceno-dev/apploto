using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Transactions;
using Xunit;

namespace WebApi.Test.Sessions;

/// <summary>
/// M9.6 Phase 4: every cashbox mutation that changes a session's calculation bumps its durable LedgerRevision exactly
/// once per command, in the same transaction, and rotates the confirmation token; inputs that leave the §6.12
/// calculation unchanged (Draft rows, non-financial edits, Tab-only rows) bump nothing. Generated pair legs count once.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class SessionControllerLedgerRevisionTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task OrdinaryRows_ShouldBumpOnlyWhenTheirActiveContributionChanges()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "RevisionOrdinary", Role.Manager);
        var inflow = await SeedInflowTypeAsync(ctx);
        var baseline = await Revision(ctx);

        var active = await Create(ctx, inflow, 50m, saveAsDraft: false);
        await ShouldBump(ctx, baseline, 1);

        var draft = await Create(ctx, inflow, 30m, saveAsDraft: true);
        await ShouldBump(ctx, baseline, 1);

        var updateDraft = await _client.PutAuthAsync(
            $"/transaction/{active.Id}",
            new RequestUpdateTransactionJsonBuilder().WithDescription("Só a descrição").WithDueDate(ctx.Today).WithTransactionTime(null).Build(),
            ctx.Token,
            expectedVersion: active.Version);
        updateDraft.StatusCode.ShouldBe(HttpStatusCode.OK, await updateDraft.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 1);

        var finalize = await _client.PostAuthAsync($"/transaction/{draft.Id}/finalize", ctx.Token, expectedVersion: draft.Version);
        finalize.StatusCode.ShouldBe(HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 2);

        var draftToCancel = await Create(ctx, inflow, 10m, saveAsDraft: true);
        var cancelDraft = await Cancel(ctx, draftToCancel.Id, draftToCancel.Version);
        cancelDraft.StatusCode.ShouldBe(HttpStatusCode.OK, await cancelDraft.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 2);

        var reloadedActive = (await factory.ReloadAsync<Transaction>(active.Id))!;
        var cancelActive = await Cancel(ctx, active.Id, reloadedActive.Version);
        cancelActive.StatusCode.ShouldBe(HttpStatusCode.OK, await cancelActive.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 3);
    }

    [Fact]
    public async Task GeneratedPairs_ShouldBumpTheCashboxSessionOnce_AndTabOnlyRowsShouldNot()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "RevisionPairs", Role.Manager);
        var baseline = await Revision(ctx);

        var sale = await _client.PostAuthAsync("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 5m, handsOutCountedStock: true), ctx.Token);
        sale.StatusCode.ShouldBe(HttpStatusCode.Created, await sale.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 1);

        var plainSale = await _client.PostAuthAsync("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 7m, handsOutCountedStock: false), ctx.Token);
        plainSale.StatusCode.ShouldBe(HttpStatusCode.Created, await plainSale.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 1);

        var cashRepayment = await _client.PostAuthAsync("/transaction/fiado-payment", TransactionControllerFiadoLinkedMovementTest.Payment(ctx, ctx.CashMethodId, 3m), ctx.Token);
        cashRepayment.StatusCode.ShouldBe(HttpStatusCode.Created, await cashRepayment.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 2);

        var nonCash = await _client.PostAuthAsync("/transaction/fiado-payment", TransactionControllerFiadoLinkedMovementTest.Payment(ctx, ctx.NonCashMethodId, 2m), ctx.Token);
        nonCash.StatusCode.ShouldBe(HttpStatusCode.Created, await nonCash.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 2);

        // Cancelling the source cancels its generated leg in the same command: one bump.
        var source = await sale.ReadContentAsync<ResponseCreateTransactionJson>();
        var sourceRow = (await factory.ReloadAsync<Transaction>(source.Id))!;
        var cancel = await Cancel(ctx, source.Id, sourceRow.Version);
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 3);

        // A Draft pair is finalized together: one bump.
        var draftSale = await _client.PostAuthAsync("/transaction", TransactionControllerFiadoLinkedMovementTest.Sale(ctx, 4m, handsOutCountedStock: true, saveAsDraft: true), ctx.Token);
        draftSale.StatusCode.ShouldBe(HttpStatusCode.Created, await draftSale.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 3);
        var draftSource = await draftSale.ReadContentAsync<ResponseCreateTransactionJson>();
        var finalize = await _client.PostAuthAsync($"/transaction/{draftSource.Id}/finalize", ctx.Token, expectedVersion: draftSource.Version);
        finalize.StatusCode.ShouldBe(HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 4);
    }

    [Fact]
    public async Task InstallmentPlans_ShouldBumpOncePerCommand_WhateverTheRowCount()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "RevisionPlans", Role.Manager);
        var category = await factory.SeedCategoryAsync(ctx.BranchId, "Cheques", Direction.Out);
        var cheque = await factory.SeedTransactionTypeAsync(category.Id, "Cheque", SettlementRule.OperatorEnteredCheque);
        var baseline = await Revision(ctx);

        var active = await _client.PostAuthAsync(
            "/transaction/installment",
            new RequestCreateTransactionInstallmentJsonBuilder()
                .WithDate(ctx.Today).WithValue(300m).WithAccountId(ctx.TerminalId).WithTransactionTypeId(cheque.Id).Build(),
            ctx.Token);
        active.StatusCode.ShouldBe(HttpStatusCode.Created, await active.Content.ReadAsStringAsync());
        (await active.ReadContentAsync<ResponseCreateTransactionInstallmentJson>()).Installments.Count.ShouldBeGreaterThan(1);
        await ShouldBump(ctx, baseline, 1);

        var draft = await _client.PostAuthAsync(
            "/transaction/installment",
            new RequestCreateTransactionInstallmentJsonBuilder()
                .WithDate(ctx.Today).WithValue(300m).WithAccountId(ctx.TerminalId).WithTransactionTypeId(cheque.Id).WithSaveAsDraft(true).Build(),
            ctx.Token);
        draft.StatusCode.ShouldBe(HttpStatusCode.Created, await draft.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 1);

        var originId = (await draft.ReadContentAsync<ResponseCreateTransactionInstallmentJson>()).Installments[0].Id;
        var plan = await (await _client.GetAuthAsync($"/transaction/installment/{originId}", ctx.Token)).ReadContentAsync<ResponseInstallmentPlanJson>();
        var finalize = await _client.PostAuthAsync(
            $"/transaction/installment/{originId}/finalize",
            new RequestFinalizeInstallmentPlanJson
            {
                Installments = [.. plan.Installments.Select(row => new RequestInstallmentPlanRowVersionJson { TransactionId = row.Id, Version = row.Version })]
            },
            ctx.Token);
        finalize.StatusCode.ShouldBe(HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        await ShouldBump(ctx, baseline, 2);
    }

    [Fact]
    public async Task RefusedMutation_ShouldLeaveTheRevisionAndTokenUntouched()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "RevisionRefused", Role.Manager);
        var inflow = await SeedInflowTypeAsync(ctx);
        var baseline = await Revision(ctx);
        await factory.SeedSettingAsync(ctx.BranchId, lockDate: ctx.Today);

        var refused = await _client.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJsonBuilder().WithAccountId(ctx.TerminalId).WithTransactionTypeId(inflow.Id)
                .WithDate(ctx.Today).WithValue(10m).WithDueDate(null).Build(),
            ctx.Token);

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ShouldBump(ctx, baseline, 0);
    }

    private async Task<TransactionType> SeedInflowTypeAsync(FiadoLinkedMovementTestContext ctx)
    {
        var category = await factory.SeedCategoryAsync(ctx.BranchId, "Entradas do caixa", Direction.In);
        return await factory.SeedTransactionTypeAsync(category.Id, "Recebimento");
    }

    private async Task<ResponseCreateTransactionJson> Create(FiadoLinkedMovementTestContext ctx, TransactionType type, decimal value, bool saveAsDraft)
    {
        var response = await _client.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJsonBuilder().WithAccountId(ctx.TerminalId).WithTransactionTypeId(type.Id)
                .WithDate(ctx.Today).WithValue(value).WithDueDate(null).WithSaveAsDraft(saveAsDraft).Build(),
            ctx.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseCreateTransactionJson>();
    }

    private Task<HttpResponseMessage> Cancel(FiadoLinkedMovementTestContext ctx, Guid transactionId, uint version)
    {
        return _client.PostAuthAsync(
            $"/transaction/{transactionId}/cancel",
            new RequestCancelTransactionJson { CancellationReason = "Lançado por engano" },
            ctx.Token,
            expectedVersion: version);
    }

    private async Task<(long Ledger, Guid Token)> Revision(FiadoLinkedMovementTestContext ctx)
    {
        var session = (await factory.ReloadAsync<DailyClose>(ctx.CloseId))!;
        return (session.LedgerRevision, session.CurrentConfirmationToken);
    }

    private async Task ShouldBump(FiadoLinkedMovementTestContext ctx, (long Ledger, Guid Token) baseline, long expectedBumps)
    {
        var session = (await factory.ReloadAsync<DailyClose>(ctx.CloseId))!;
        session.LedgerRevision.ShouldBe(baseline.Ledger + expectedBumps);
        if (expectedBumps == 0)
            session.CurrentConfirmationToken.ShouldBe(baseline.Token);
        else
            session.CurrentConfirmationToken.ShouldNotBe(baseline.Token);
        session.CountRevision.ShouldBe(0);
    }
}
