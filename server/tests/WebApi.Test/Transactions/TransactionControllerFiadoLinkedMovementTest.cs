using CommonTestUtilities.Requests;
using System.Net;
using System.Net.Http.Json;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

/// <summary>
/// M7.7 Phase 5.1 command + 5.6 + 5.7 against real PostgreSQL: counted-stock fiado sales and <i>Receber
/// pagamento</i> write their linked, generated Terminal leg atomically; non-cash repayments write the Tab only;
/// generated rows carry structured origin and are never mutated on their own; the pair moves together.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TransactionControllerFiadoLinkedMovementTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CounterFiadoSale_ShouldWriteTheTabOutAndAGeneratedCashboxOut_InOneCommit()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPairSale", Role.Manager);

        var response = await _client.PostAuthAsync("/transaction", Sale(ctx, 5m), ctx.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.ReadContentAsync<ResponseCreateTransactionJson>();
        created.GeneratedTransactionId.ShouldNotBeNull();
        created.GenerationKind.ShouldBeNull();
        var rows = await ctx.ListBranchTransactionsAsync();
        rows.Count.ShouldBe(2);
        var source = rows.Single(row => row.Id == created.Id);
        var generated = rows.Single(row => row.Id == created.GeneratedTransactionId);
        source.AccountId.ShouldBe(ctx.TabId);
        source.Direction.ShouldBe(Direction.Out);
        generated.AccountId.ShouldBe(ctx.TerminalId);
        generated.Direction.ShouldBe(Direction.Out);
        generated.Value.ShouldBe(5m);
        generated.Date.ShouldBe(source.Date);
        generated.Status.ShouldBe(TransactionStatus.Active);
        generated.TransactionTypeId.ShouldBe(ctx.StockSaleSystemTypeId);
        generated.GenerationKind.ShouldBe(TransactionGenerationKind.FiadoSale);
        generated.GeneratedFromTransactionId.ShouldBe(source.Id);
        generated.ClientId.ShouldBeNull();
        generated.RecordedByOperatorId.ShouldBe(source.RecordedByOperatorId);
        generated.CreatedByUserId.ShouldBe(ctx.UserId);

        // Detail and list expose the structured origin; the source names its counterpart.
        var sourceDetail = await GetAsync<ResponseTransactionJson>($"/transaction/{source.Id}", ctx.Token);
        var generatedDetail = await GetAsync<ResponseTransactionJson>($"/transaction/{generated.Id}", ctx.Token);
        sourceDetail.GeneratedTransactionId.ShouldBe(generated.Id);
        generatedDetail.GenerationKind.ShouldBe(TransactionGenerationKind.FiadoSale);
        generatedDetail.GeneratedFromTransactionId.ShouldBe(source.Id);
        // SV17's type filter finds the actual generated rows by their system type.
        var byType = await GetAsync<ResponseListTransactionsJson>(
            $"/transaction?transactionTypeId={ctx.StockSaleSystemTypeId}", ctx.Token);
        var item = byType.Items.ShouldHaveSingleItem();
        item.Id.ShouldBe(generated.Id);
        item.GenerationKind.ShouldBe(TransactionGenerationKind.FiadoSale);
        item.GeneratedFromTransactionId.ShouldBe(source.Id);
    }

    [Fact]
    public async Task EveryCounterFiadoSale_ShouldPair_WithNoCallerSwitch()
    {
        // M9.6 Phase 6: the counted-stock declaration is gone. A bill/game sale on fiado leaves the expected cash
        // unreceived exactly like a stock sale (§6.19 O05), so every new counter sale writes the cashbox Out.
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPairAlways", Role.Manager);

        var response = await _client.PostAuthAsync("/transaction", Sale(ctx, 40m), ctx.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.ReadContentAsync<ResponseCreateTransactionJson>();
        var rows = await ctx.ListBranchTransactionsAsync();
        rows.Count.ShouldBe(2);
        var generated = rows.Single(row => row.Id == created.GeneratedTransactionId);
        generated.AccountId.ShouldBe(ctx.TerminalId);
        generated.Direction.ShouldBe(Direction.Out);
        generated.Value.ShouldBe(40m);
        generated.SessionId.ShouldNotBeNull();
        rows.Single(row => row.Id == created.Id).SessionId.ShouldBeNull();
    }

    [Fact]
    public async Task FiadoSale_ShouldBeRefusedWithoutAClient_AndWriteNothing()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoSaleNoClient", Role.Manager);
        var outCategory = await factory.SeedCategoryAsync(ctx.BranchId, "Saídas avulsas", Direction.Out);
        var ordinary = await factory.SeedTransactionTypeAsync(outCategory.Id, "Saída sem cliente");

        var response = await _client.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJson { Date = ctx.Today, Value = 10m, TransactionTypeId = ordinary.Id, AccountId = ctx.TabId },
            ctx.Token);

        await ShouldFail(response, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_SALE_CLIENT_REQUIRED);
        (await ctx.ListBranchTransactionsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task FiadoInstallmentPlan_ShouldBeRefused_BecauseItWouldBeAnUnpairedSale()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoSalePlan", Role.Manager);
        var category = await factory.SeedCategoryAsync(ctx.BranchId, "Saídas cheque", Direction.Out);
        var cheque = await factory.SeedTransactionTypeAsync(
            category.Id, settlementRule: SettlementRule.OperatorEnteredCheque, requiresTabAccountAndClient: true);
        var request = new RequestCreateTransactionInstallmentJsonBuilder()
            .WithDate(ctx.Today)
            .WithValue(300m)
            .WithTransactionTypeId(cheque.Id)
            .WithAccountId(ctx.TabId)
            .WithClientId(ctx.ClientId)
            .WithAutoGeneration(3, ctx.Today.AddDays(30))
            .Build();

        await ShouldFail(await _client.PostAuthAsync("/transaction/installment", request, ctx.Token),
            HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_SALE_REQUIRES_SINGLE_CREATE);
        await ShouldFail(await _client.PostAuthAsync("/transaction/installment/preview", request, ctx.Token),
            HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_SALE_REQUIRES_SINGLE_CREATE);
        (await ctx.ListBranchTransactionsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task CashRepayment_ShouldWriteTheTabInAndAGeneratedTerminalIn()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPairCash", Role.Manager);

        var response = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(ctx, ctx.CashMethodId, 5m), ctx.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        response.Headers.ETag.ShouldNotBeNull();
        var created = await response.ReadContentAsync<ResponseCreateTransactionJson>();
        created.Direction.ShouldBe(Direction.In);
        created.AccountId.ShouldBe(ctx.TabId);
        created.ClientId.ShouldBe(ctx.ClientId);
        var generated = (await ctx.ListBranchTransactionsAsync()).Single(row => row.Id == created.GeneratedTransactionId);
        generated.AccountId.ShouldBe(ctx.TerminalId);
        generated.Direction.ShouldBe(Direction.In);
        generated.Value.ShouldBe(5m);
        generated.TransactionTypeId.ShouldBe(ctx.CashRepaymentSystemTypeId);
        generated.GenerationKind.ShouldBe(TransactionGenerationKind.FiadoCashRepayment);
        generated.GeneratedFromTransactionId.ShouldBe(created.Id);
    }

    [Fact]
    public async Task NonCashRepayment_ShouldWriteOnlyTheTabIn_AndNeedNoClose()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPairPix", Role.Member, grantTerminal: false, openClose: false);

        var response = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(ctx, ctx.NonCashMethodId, 30m), ctx.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<ResponseCreateTransactionJson>()).GeneratedTransactionId.ShouldBeNull();
        var row = (await ctx.ListBranchTransactionsAsync()).ShouldHaveSingleItem();
        row.AccountId.ShouldBe(ctx.TabId);
        row.Direction.ShouldBe(Direction.In);
        row.GenerationKind.ShouldBeNull();
    }

    [Fact]
    public async Task Payment_ShouldRequireAConfiguredMethod_WithNoDefault()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPayMethod", Role.Manager);

        var missing = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(ctx, Guid.Empty, 10m), ctx.Token);
        var unconfigured = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(ctx, ctx.UnconfiguredRepaymentTypeId, 10m), ctx.Token);
        var saleType = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(ctx, ctx.SaleTypeId, 10m), ctx.Token);
        var systemType = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(ctx, ctx.CashRepaymentSystemTypeId, 10m), ctx.Token);
        var noClient = await _client.PostAuthAsync(
            "/transaction/fiado-payment",
            new RequestReceiveFiadoPaymentJson { Date = ctx.Today, Value = 10m, TransactionTypeId = ctx.CashMethodId, AccountId = ctx.TabId },
            ctx.Token);

        await ShouldFail(missing, HttpStatusCode.BadRequest, ResourcesErrorMessages.TRANSACTION_FIADO_PAYMENT_METHOD_INVALID);
        await ShouldFail(unconfigured, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_PAYMENT_METHOD_INVALID);
        await ShouldFail(saleType, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_PAYMENT_METHOD_INVALID);
        await ShouldFail(systemType, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_PAYMENT_METHOD_INVALID);
        await ShouldFail(noClient, HttpStatusCode.BadRequest, ResourcesErrorMessages.TRANSACTION_FIADO_PAYMENT_CLIENT_REQUIRED);
        (await ctx.ListBranchTransactionsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task GenericCreate_ShouldNotBypassTheCommandOrWriteSystemTypes()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoBypass", Role.Manager);
        var tabIn = new RequestCreateTransactionJson
        {
            Date = ctx.Today, Value = 10m, TransactionTypeId = ctx.CashMethodId, AccountId = ctx.TabId, ClientId = ctx.ClientId
        };
        var systemOnTerminal = new RequestCreateTransactionJson
        {
            Date = ctx.Today, Value = 10m, TransactionTypeId = ctx.StockSaleSystemTypeId, AccountId = ctx.TerminalId
        };

        await ShouldFail(await _client.PostAuthAsync("/transaction", tabIn, ctx.Token), HttpStatusCode.Conflict,
            ResourcesErrorMessages.TRANSACTION_FIADO_PAYMENT_REQUIRES_COMMAND);
        await ShouldFail(await _client.PostAuthAsync("/transaction/preview", tabIn, ctx.Token), HttpStatusCode.Conflict,
            ResourcesErrorMessages.TRANSACTION_FIADO_PAYMENT_REQUIRES_COMMAND);
        await ShouldFail(await _client.PostAuthAsync("/transaction", systemOnTerminal, ctx.Token), HttpStatusCode.Conflict,
            ResourcesErrorMessages.TRANSACTION_TYPE_SYSTEM_ONLY);
        (await ctx.ListBranchTransactionsAsync()).ShouldBeEmpty();

        // The generated cashbox legs' system types are never written by hand either.
        var repaymentLeg = await _client.PostAuthAsync(
            "/transaction",
            new RequestCreateTransactionJson
            {
                Date = ctx.Today, Value = 10m, TransactionTypeId = ctx.CashRepaymentSystemTypeId, AccountId = ctx.TerminalId
            },
            ctx.Token);
        await ShouldFail(repaymentLeg, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_SYSTEM_ONLY);
    }

    [Fact]
    public async Task PhysicalPair_ShouldWriteNeitherRow_WhenTheTerminalCannotTakeIt()
    {
        var unpaired = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPairUnpaired", Role.Manager, pairTab: false);
        var noClose = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPairNoClose", Role.Manager, openClose: false);
        var submitted = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPairSubmitted", Role.Manager);
        await submitted.SetCloseStatusAsync(DailyCloseStatus.Submitted);

        await ShouldFail(
            await _client.PostAuthAsync("/transaction", Sale(unpaired, 5m), unpaired.Token),
            HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_PAIRED_TERMINAL_REQUIRED);
        await ShouldFail(
            await _client.PostAuthAsync("/transaction/fiado-payment", Payment(unpaired, unpaired.CashMethodId, 5m), unpaired.Token),
            HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_FIADO_PAIRED_TERMINAL_REQUIRED);
        await ShouldFail(
            await _client.PostAuthAsync("/transaction", Sale(noClose, 5m), noClose.Token),
            HttpStatusCode.Conflict, ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_REQUIRED);
        await ShouldFail(
            await _client.PostAuthAsync("/transaction/fiado-payment", Payment(submitted, submitted.CashMethodId, 5m), submitted.Token),
            HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);

        (await unpaired.ListBranchTransactionsAsync()).ShouldBeEmpty();
        (await noClose.ListBranchTransactionsAsync()).ShouldBeEmpty();
        (await submitted.ListBranchTransactionsAsync()).ShouldBeEmpty();
        // A non-cash payment on the same frozen day only touches the Tab, so it is accepted.
        var pix = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(submitted, submitted.NonCashMethodId, 5m), submitted.Token);
        pix.StatusCode.ShouldBe(HttpStatusCode.Created, await pix.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PhysicalPair_ShouldRequireBothExplicitGrants_ForAMember()
    {
        var tabOnly = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoMemberTabOnly", Role.Member, grantTerminal: false);
        var terminalOnly = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoMemberTerminalOnly", Role.Member, grantTab: false);
        var both = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoMemberBoth", Role.Member);

        await ShouldFail(
            await _client.PostAuthAsync("/transaction", Sale(tabOnly, 5m), tabOnly.Token),
            HttpStatusCode.Forbidden, ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        await ShouldFail(
            await _client.PostAuthAsync("/transaction/fiado-payment", Payment(tabOnly, tabOnly.CashMethodId, 5m), tabOnly.Token),
            HttpStatusCode.Forbidden, ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        // A Terminal grant never implies fiado authority (decision 1.8).
        await ShouldFail(
            await _client.PostAuthAsync("/transaction/fiado-payment", Payment(terminalOnly, terminalOnly.CashMethodId, 5m), terminalOnly.Token),
            HttpStatusCode.Forbidden, ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        (await tabOnly.ListBranchTransactionsAsync()).ShouldBeEmpty();
        (await terminalOnly.ListBranchTransactionsAsync()).ShouldBeEmpty();

        var sale = await _client.PostAuthAsync("/transaction", Sale(both, 5m), both.Token);
        var cash = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(both, both.CashMethodId, 5m), both.Token);
        sale.StatusCode.ShouldBe(HttpStatusCode.Created, await sale.Content.ReadAsStringAsync());
        cash.StatusCode.ShouldBe(HttpStatusCode.Created, await cash.Content.ReadAsStringAsync());
        (await both.ListBranchTransactionsAsync()).Count.ShouldBe(4);
    }

    [Fact]
    public async Task GeneratedRow_ShouldRefuseEveryIndependentMutation()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoGeneratedReadOnly", Role.Manager);
        var created = await CreateSaleAsync(ctx, 5m, saveAsDraft: true);
        var generated = (await ctx.ListBranchTransactionsAsync()).Single(row => row.Id == created.GeneratedTransactionId);

        var update = await _client.PutAuthAsync(
            $"/transaction/{generated.Id}",
            new RequestUpdateTransactionJson { Description = "editado", DueDate = generated.DueDate },
            ctx.Token,
            expectedVersion: generated.Version);
        var finalize = await _client.PostAuthAsync($"/transaction/{generated.Id}/finalize", ctx.Token, expectedVersion: generated.Version);
        var cancel = await _client.PostAuthAsync(
            $"/transaction/{generated.Id}/cancel",
            new RequestCancelTransactionJson { CancellationReason = "teste" },
            ctx.Token,
            expectedVersion: generated.Version);

        await ShouldFail(update, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_GENERATED_ROW_READ_ONLY);
        await ShouldFail(finalize, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_GENERATED_ROW_READ_ONLY);
        await ShouldFail(cancel, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_GENERATED_ROW_READ_ONLY);
        (await factory.ReloadAsync<Transaction>(generated.Id))!.Status.ShouldBe(TransactionStatus.Draft);
    }

    [Fact]
    public async Task DraftPair_ShouldFinalizeAndCancelTogether_ThroughTheSource()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPairLifecycle", Role.Manager);
        var created = await CreateSaleAsync(ctx, 5m, saveAsDraft: true);
        var generatedId = created.GeneratedTransactionId!.Value;
        (await factory.ReloadAsync<Transaction>(generatedId))!.Status.ShouldBe(TransactionStatus.Draft);

        var finalize = await _client.PostAuthAsync($"/transaction/{created.Id}/finalize", ctx.Token, expectedVersion: created.Version);
        finalize.StatusCode.ShouldBe(HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        var finalized = await finalize.ReadContentAsync<ResponseTransactionJson>();
        finalized.GeneratedTransactionId.ShouldBe(generatedId);
        (await factory.ReloadAsync<Transaction>(generatedId))!.Status.ShouldBe(TransactionStatus.Active);

        var cancel = await _client.PostAuthAsync(
            $"/transaction/{created.Id}/cancel",
            new RequestCancelTransactionJson { CancellationReason = "Venda registrada por engano" },
            ctx.Token,
            expectedVersion: finalized.Version);
        cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        var generated = (await factory.ReloadAsync<Transaction>(generatedId))!;
        generated.Status.ShouldBe(TransactionStatus.Cancelled);
        generated.CancellationReason.ShouldBe("Venda registrada por engano");
    }

    [Fact]
    public async Task PairLifecycle_ShouldBeBlockedTogether_WhenTheTerminalDayIsFrozen()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPairFrozenFinalize", Role.Manager);
        var created = await CreateSaleAsync(ctx, 5m, saveAsDraft: true);
        await ctx.SetCloseStatusAsync(DailyCloseStatus.Submitted);

        var finalize = await _client.PostAuthAsync($"/transaction/{created.Id}/finalize", ctx.Token, expectedVersion: created.Version);

        await ShouldFail(finalize, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
        (await factory.ReloadAsync<Transaction>(created.Id))!.Status.ShouldBe(TransactionStatus.Draft);
        (await factory.ReloadAsync<Transaction>(created.GeneratedTransactionId!.Value))!.Status.ShouldBe(TransactionStatus.Draft);
    }

    [Fact]
    public async Task Payment_ShouldReplayTheWholeOperation_IncludingAfterTheDayFreezes()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPayReplay", Role.Manager);
        const string key = "fiado-payment-replay-1";

        var first = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(ctx, ctx.CashMethodId, 5m), ctx.Token, idempotencyKey: key);
        await ctx.SetCloseStatusAsync(DailyCloseStatus.Submitted);
        var replay = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(ctx, ctx.CashMethodId, 5m), ctx.Token, idempotencyKey: key);
        var differentPayload = await _client.PostAuthAsync("/transaction/fiado-payment", Payment(ctx, ctx.CashMethodId, 6m), ctx.Token, idempotencyKey: key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        replay.StatusCode.ShouldBe(HttpStatusCode.Created, await replay.Content.ReadAsStringAsync());
        var original = await first.ReadContentAsync<ResponseCreateTransactionJson>();
        var replayed = await replay.ReadContentAsync<ResponseCreateTransactionJson>();
        replayed.Id.ShouldBe(original.Id);
        replayed.GeneratedTransactionId.ShouldBe(original.GeneratedTransactionId);
        differentPayload.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await ctx.ListBranchTransactionsAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Payment_ShouldWriteExactlyOnePair_UnderConcurrentSameKeyRequests()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPayConcurrent", Role.Manager);
        const string key = "fiado-payment-concurrent-1";

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
        {
            using var client = factory.CreateClient();
            return factory.CreateClient().PostAuthAsync(
                "/transaction/fiado-payment", Payment(ctx, ctx.CashMethodId, 5m), ctx.Token, idempotencyKey: key);
        }));

        responses.ShouldAllBe(response => response.StatusCode == HttpStatusCode.Created || response.StatusCode == HttpStatusCode.Conflict);
        responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBeGreaterThanOrEqualTo(1);
        var rows = await ctx.ListBranchTransactionsAsync();
        rows.Count.ShouldBe(2);
        rows.Count(row => row.GenerationKind == TransactionGenerationKind.FiadoCashRepayment).ShouldBe(1);
    }

    [Fact]
    public async Task Payment_ShouldLeaveNoRowOrReservation_WhenTheGeneratedLegCannotBeStored()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPayRollback", Role.Manager);
        var suffix = ctx.BranchId.ToString("N");
        const string key = "fiado-payment-rollback-1";
        await ctx.ExecuteSqlAsync(
            $$"""
            CREATE FUNCTION reject_generated_{{suffix}}() RETURNS trigger AS $$
            BEGIN
                IF NEW."BranchId" = '{{ctx.BranchId}}' AND NEW."GenerationKind" IS NOT NULL THEN
                    RAISE EXCEPTION 'generated leg store unavailable (test)';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER reject_generated_{{suffix}}
            BEFORE INSERT ON "Transactions"
            FOR EACH ROW EXECUTE FUNCTION reject_generated_{{suffix}}();
            """);
        try
        {
            var failed = await _client.PostAuthAsync(
                "/transaction/fiado-payment", Payment(ctx, ctx.CashMethodId, 5m), ctx.Token, idempotencyKey: key);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        finally
        {
            await ctx.ExecuteSqlAsync(
                $"""
                DROP TRIGGER IF EXISTS reject_generated_{suffix} ON "Transactions";
                DROP FUNCTION IF EXISTS reject_generated_{suffix}();
                """);
        }

        (await ctx.ListBranchTransactionsAsync()).ShouldBeEmpty();
        var retry = await _client.PostAuthAsync(
            "/transaction/fiado-payment", Payment(ctx, ctx.CashMethodId, 5m), ctx.Token, idempotencyKey: key);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created, await retry.Content.ReadAsStringAsync());
        (await ctx.ListBranchTransactionsAsync()).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Create_ShouldIgnoreForgedOriginFields()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoForgedOrigin", Role.Manager);
        var victim = await CreateSaleAsync(ctx, 9m, saveAsDraft: false);
        var forged = new
        {
            Date = ctx.Today,
            Value = 7m,
            TransactionTypeId = ctx.SaleTypeId,
            AccountId = ctx.TabId,
            ClientId = ctx.ClientId,
            GenerationKind = (int)TransactionGenerationKind.FiadoSale,
            GeneratedFromTransactionId = victim.Id
        };

        var response = await _client.PostAuthAsync("/transaction", forged, ctx.Token, idempotencyKey: "forged-origin-1");

        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = (await factory.ReloadAsync<Transaction>((await response.ReadContentAsync<ResponseCreateTransactionJson>()).Id))!;
        created.GenerationKind.ShouldBeNull();
        created.GeneratedFromTransactionId.ShouldBeNull();
    }

    [Fact]
    public async Task SystemTypesAndCategories_ShouldBeListedButNeverManaged()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoSystemTypes", Role.Manager);
        var systemCategoryId = (await factory.ReloadAsync<TransactionType>(ctx.StockSaleSystemTypeId))!.CategoryId;
        var outCategory = await factory.SeedCategoryAsync(ctx.BranchId, "Despesas", Direction.Out);

        var list = await GetAsync<ResponseListTransactionTypesJson>("/transaction-type", ctx.Token);
        var update = await _client.PutAuthAsync(
            $"/transaction-type/{ctx.StockSaleSystemTypeId}",
            new RequestUpdateTransactionTypeJson { Name = "Renomeado" },
            ctx.Token);
        var deactivate = await _client.DeleteAuthAsync($"/transaction-type/{ctx.StockSaleSystemTypeId}", ctx.Token);
        var deactivateCategory = await _client.DeleteAuthAsync($"/category/{systemCategoryId}", ctx.Token);
        var intoSystemCategory = await _client.PostAuthAsync(
            "/transaction-type",
            new RequestCreateTransactionTypeJson { CategoryId = systemCategoryId, Name = "Intruso" },
            ctx.Token);
        var kindOnOut = await _client.PostAuthAsync(
            "/transaction-type",
            new RequestCreateTransactionTypeJson
            {
                CategoryId = outCategory.Id, Name = "Saída como recebimento", RequiresTabAccountAndClient = true,
                FiadoPaymentKind = FiadoPaymentKind.CountedCash
            },
            ctx.Token);

        list.Items.Single(type => type.Id == ctx.StockSaleSystemTypeId).SystemRole
            .ShouldBe(TransactionTypeSystemRole.FiadoSaleCounterpart);
        list.Items.Single(type => type.Id == ctx.CashMethodId).FiadoPaymentKind.ShouldBe(FiadoPaymentKind.CountedCash);
        await ShouldFail(update, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_SYSTEM_READ_ONLY);
        await ShouldFail(deactivate, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_SYSTEM_READ_ONLY);
        await ShouldFail(deactivateCategory, HttpStatusCode.Conflict, ResourcesErrorMessages.CATEGORY_SYSTEM_READ_ONLY);
        await ShouldFail(intoSystemCategory, HttpStatusCode.Conflict, ResourcesErrorMessages.CATEGORY_SYSTEM_READ_ONLY);
        await ShouldFail(kindOnOut, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_FIADO_PAYMENT_KIND_INVALID);
        (await factory.ReloadAsync<TransactionType>(ctx.StockSaleSystemTypeId))!.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task SelfContext_ShouldNameTheWorkstationsPairedTab_WithoutGrantingIt()
    {
        var withoutTab = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoSelfNoTab", Role.Member, grantTab: false);
        var withTab = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoSelfTab", Role.Member);

        var junior = await GetAsync<ResponseSelfContextJson>("/operator/self-context", withoutTab.Token);
        var senior = await GetAsync<ResponseSelfContextJson>("/operator/self-context", withTab.Token);

        // The routing target is visible from the operator's own context; the grant still decides who may record.
        junior.PrimaryAccount!.AccountId.ShouldBe(withoutTab.TerminalId);
        junior.PrimaryAccount.PairedTabAccountId.ShouldBe(withoutTab.TabId);
        junior.AvailableAccounts.Select(account => account.AccountId).ShouldNotContain(withoutTab.TabId);
        senior.AvailableAccounts.Single(account => account.AccountId == withTab.TerminalId)
            .PairedTabAccountId.ShouldBe(withTab.TabId);
        senior.AvailableAccounts.Single(account => account.AccountId == withTab.TabId).PairedTabAccountId.ShouldBeNull();
    }

    [Fact]
    public async Task Preview_ShouldValidateAndProjectTheWholePhysicalSale_LikeItsWriteTwin()
    {
        var tabOnly = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPreviewTabOnly", Role.Member, grantTerminal: false);
        var noClose = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPreviewNoClose", Role.Manager, openClose: false);
        var both = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPreviewBoth", Role.Member);

        await ShouldFail(
            await _client.PostAuthAsync("/transaction/preview", Sale(tabOnly, 5m), tabOnly.Token),
            HttpStatusCode.Forbidden, ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
        await ShouldFail(
            await _client.PostAuthAsync("/transaction/preview", Sale(noClose, 5m), noClose.Token),
            HttpStatusCode.Conflict, ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_REQUIRED);

        var preview = await _client.PostAuthAsync("/transaction/preview", Sale(both, 5m), both.Token);

        preview.StatusCode.ShouldBe(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var impact = (await preview.ReadContentAsync<ResponseCreateTransactionPreviewJson>()).Impact;
        // The affected close is the paired terminal's; the generated Terminal Out moves its variance by +5.
        impact.CashVarianceImpact.AccountId.ShouldBe(both.TerminalId);
        impact.CashVarianceImpact.DailyCloseStatus.ShouldBe(DailyCloseStatus.Draft);
        impact.CashVarianceImpact.VarianceDelta.ShouldBe(5m);
        (await both.ListBranchTransactionsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Preview_ShouldNotClaimAgingMembership_WhenClientCreditAbsorbsTheSale()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPreviewCredit", Role.Manager);
        await _client.PostAuthAsync("/transaction/fiado-payment", Payment(ctx, ctx.NonCashMethodId, 20m), ctx.Token);

        var preview = await _client.PostAuthAsync("/transaction/preview", Sale(ctx, 5m), ctx.Token);
        var created = await CreateSaleAsync(ctx, 5m, saveAsDraft: false);
        var aging = await GetAsync<ResponseFiadoAgingJson>($"/report/fiado/aging?clientId={ctx.ClientId}&page=1&pageSize=50", ctx.Token);

        preview.StatusCode.ShouldBe(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        var receivable = (await preview.ReadContentAsync<ResponseCreateTransactionPreviewJson>()).Impact.ReceivableImpact;
        receivable.RowAppearsInOpenReceivables.ShouldBeFalse();
        receivable.BucketAfter.ShouldBeNull();
        aging.Items.Select(item => item.TransactionId).ShouldNotContain(created.Id);
        aging.TotalCount.ShouldBe(0);

        // Without credit the same preview does report the sale entering aging, and the write agrees.
        var debtor = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoPreviewDebtor", Role.Manager);
        var debtorPreview = await _client.PostAuthAsync("/transaction/preview", Sale(debtor, 5m), debtor.Token);
        (await debtorPreview.ReadContentAsync<ResponseCreateTransactionPreviewJson>()).Impact.ReceivableImpact
            .RowAppearsInOpenReceivables.ShouldBeTrue();
        var debtorSale = await CreateSaleAsync(debtor, 5m, saveAsDraft: false);
        (await GetAsync<ResponseFiadoAgingJson>($"/report/fiado/aging?clientId={debtor.ClientId}&page=1&pageSize=50", debtor.Token))
            .Items.ShouldHaveSingleItem().TransactionId.ShouldBe(debtorSale.Id);
    }

    [Fact]
    public async Task SourceUpdate_ShouldKeepNamingItsGeneratedLeg()
    {
        var ctx = await FiadoLinkedMovementTestContext.SeedAsync(factory, "FiadoSourceUpdate", Role.Manager);
        var created = await CreateSaleAsync(ctx, 5m, saveAsDraft: false);
        var source = (await factory.ReloadAsync<Transaction>(created.Id))!;

        var update = await _client.PutAuthAsync(
            $"/transaction/{created.Id}",
            new RequestUpdateTransactionJson { Description = "Raspadinha", DueDate = source.DueDate, ClientId = ctx.ClientId },
            ctx.Token,
            expectedVersion: created.Version);

        update.StatusCode.ShouldBe(HttpStatusCode.OK, await update.Content.ReadAsStringAsync());
        (await update.ReadContentAsync<ResponseTransactionJson>()).GeneratedTransactionId.ShouldBe(created.GeneratedTransactionId);
        (await GetAsync<ResponseTransactionJson>($"/transaction/{created.Id}", ctx.Token)).GeneratedTransactionId
            .ShouldBe(created.GeneratedTransactionId);
    }

    private async Task<ResponseCreateTransactionJson> CreateSaleAsync(
        FiadoLinkedMovementTestContext ctx,
        decimal value,
        bool saveAsDraft)
    {
        var response = await _client.PostAuthAsync("/transaction", Sale(ctx, value, saveAsDraft), ctx.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseCreateTransactionJson>();
    }

    private async Task<T> GetAsync<T>(string url, string token)
        where T : class
    {
        var response = await _client.GetAuthAsync(url, token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<T>();
    }

    private static async Task ShouldFail(HttpResponseMessage response, HttpStatusCode status, string key)
    {
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(key);
    }

    internal static RequestCreateTransactionJson Sale(
        FiadoLinkedMovementTestContext ctx,
        decimal value,
        bool saveAsDraft = false)
    {
        return new RequestCreateTransactionJson
        {
            Date = ctx.Today,
            Value = value,
            TransactionTypeId = ctx.SaleTypeId,
            AccountId = ctx.TabId,
            ClientId = ctx.ClientId,
            DueDate = ctx.Today.AddDays(30),
            SaveAsDraft = saveAsDraft
        };
    }

    internal static RequestReceiveFiadoPaymentJson Payment(FiadoLinkedMovementTestContext ctx, Guid methodId, decimal value)
    {
        return new RequestReceiveFiadoPaymentJson
        {
            Date = ctx.Today,
            Value = value,
            TransactionTypeId = methodId,
            AccountId = ctx.TabId,
            ClientId = ctx.ClientId
        };
    }
}
