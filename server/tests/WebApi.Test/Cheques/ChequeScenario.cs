using System.Net;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;

namespace WebApi.Test.Cheques;

/// <summary>
/// M9.7 Phase 2 fixture on a branch seeded by the real bootstrap: the financial-day branch (cashbox with its Tab, cofre,
/// client, Managers) plus a bank account and the seeded <i>Depósito Cheque</i> type (instrument Cheque). Cheque rows
/// are seeded on the cashbox in past months (the database captures their frozen classification on insert); the lock
/// date is set directly, since locking a month is exercised by its own tests.
/// </summary>
internal sealed class ChequeScenario
{
    private readonly ServerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private ChequeScenario(ServerWebApplicationFactory factory, HttpClient client, FinancialDayScenario day, Account bank)
    {
        _factory = factory;
        _client = client;
        Day = day;
        Bank = bank;
    }

    public FinancialDayScenario Day { get; }
    public Account Bank { get; }
    public Guid BranchId => Day.Branch.Id;
    public string ManagerToken => Day.Manager.Token;
    public TransactionType ChequeType => Day.SeededTypes["saidas.deposito-cheque"];

    public static async Task<ChequeScenario> CreateAsync(ServerWebApplicationFactory factory, HttpClient client, string label)
    {
        var day = await FinancialDayScenario.CreateAsync(factory, client, label);
        var bank = await factory.SeedAccountAsync(day.Branch.Id, AccountType.BankAccount, $"Banco {label}");
        return new ChequeScenario(factory, client, day, bank);
    }

    public Task<Transaction> SeedChequeAsync(DateTime date, decimal value, Guid? clientId = null, DateTime? dueDate = null) =>
        _factory.SeedTransactionAsync(
            branchId: BranchId,
            accountId: Day.CashboxId,
            transactionTypeId: ChequeType.Id,
            categoryId: ChequeType.CategoryId,
            direction: Direction.Out,
            recordedByOperatorId: Day.MemberA.Operator!.Id,
            createdByUserId: Day.Manager.User.Id,
            date: date,
            value: value,
            clientId: clientId,
            dueDate: dueDate ?? date);

    public Task LockThroughAsync(DateTime lockDate) => QueryAsync(db => db.Database.ExecuteSqlInterpolatedAsync(
        $"""UPDATE "Settings" SET "LockDate" = {DateOnly.FromDateTime(lockDate)} WHERE "BranchId" = {BranchId}"""));

    public Task<T> QueryAsync<T>(Func<ServerDbContext, Task<T>> query) => Day.QueryAsync(query);

    public Task<List<SettlementEvent>> EventsAsync(Guid transactionId) => QueryAsync(db => db.SettlementEvents.AsNoTracking()
        .Where(e => e.ReceivableTransactionId == transactionId).OrderBy(e => e.Sequence).ToListAsync());

    public Task<int> BranchEventCountAsync() => QueryAsync(db => db.SettlementEvents.CountAsync(e => e.BranchId == BranchId));
    public Task<int> BranchReceiptCountAsync() => QueryAsync(db => db.BankReceipts.CountAsync(r => r.BranchId == BranchId));
    public Task<int> BranchTransactionCountAsync() => QueryAsync(db => db.Transactions.CountAsync(t => t.BranchId == BranchId));

    public Task<HttpResponseMessage> DepositAsync(
        DateTime occurredOn,
        IEnumerable<(Guid Id, int Version)> items,
        Guid? bankId = null,
        DateTime? postedOn = null,
        string? key = null,
        string? token = null) =>
        _client.PostAuthAsync(
            "/cheque/deposit",
            new RequestChequeDepositJson
            {
                BankAccountId = bankId ?? Bank.Id,
                OccurredOn = occurredOn,
                PostedOn = postedOn,
                Items = items.Select(item => new RequestChequeOperationItemJson
                {
                    TransactionId = item.Id,
                    ExpectedVersion = item.Version
                }).ToList()
            },
            token ?? ManagerToken,
            idempotencyKey: key);

    public async Task<ResponseChequeOperationJson> DepositOkAsync(DateTime occurredOn, params (Guid Id, int Version)[] items)
    {
        var response = await DepositAsync(occurredOn, items);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseChequeOperationJson>();
    }

    public Task<HttpResponseMessage> ReturnAsync(
        Guid transactionId, Guid depositEventId, int version, DateTime occurredOn, Guid clientId,
        DateTime? nextDeposit = null, string? reason = null, string? key = null) =>
        _client.PostAuthAsync(
            $"/cheque/{transactionId}/return",
            new RequestChequeReturnJson
            {
                DepositEventId = depositEventId,
                ExpectedVersion = version,
                OccurredOn = occurredOn,
                ClientId = clientId,
                NextExpectedDepositDate = nextDeposit ?? occurredOn.AddDays(7),
                ReturnReason = reason
            },
            ManagerToken,
            idempotencyKey: key);

    public Task<HttpResponseMessage> RepresentAsync(Guid transactionId, int version, DateTime occurredOn, string? key = null) =>
        _client.PostAuthAsync(
            $"/cheque/{transactionId}/represent",
            new RequestChequeRepresentJson { ExpectedVersion = version, OccurredOn = occurredOn },
            ManagerToken,
            idempotencyKey: key);

    public Task<HttpResponseMessage> ConvertAsync(Guid transactionId, int version, DateTime occurredOn, Guid clientId, Guid? tabId = null) =>
        _client.PostAuthAsync(
            $"/cheque/{transactionId}/convert-to-fiado",
            new RequestChequeConvertToFiadoJson
            {
                ExpectedVersion = version,
                OccurredOn = occurredOn,
                ClientId = clientId,
                TabAccountId = tabId ?? Day.TabId
            },
            ManagerToken);

    public Task<HttpResponseMessage> CustodyAsync(DateTime occurredOn, Guid safeId, params (Guid Id, int Version)[] items) =>
        _client.PostAuthAsync(
            "/cheque/custody",
            new RequestChequeCustodyJson
            {
                SafeAccountId = safeId,
                OccurredOn = occurredOn,
                Items = items.Select(item => new RequestChequeOperationItemJson
                {
                    TransactionId = item.Id,
                    ExpectedVersion = item.Version
                }).ToList()
            },
            ManagerToken);

    public async Task<ResponseReceivableJson> ReceivableAsync(Guid transactionId, DateTime? cutoff = null,
        ReceivableCutoffBasis basis = ReceivableCutoffBasis.Posted)
    {
        var query = cutoff is null ? $"?cutoffBasis={basis}" : $"?cutoffDate={cutoff:yyyy-MM-dd}&cutoffBasis={basis}";
        var response = await _client.GetAuthAsync($"/receivable/{transactionId}{query}", ManagerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseReceivableJson>();
    }

    public async Task<ResponseOpenChequeAgingJson> OpenChequesAsync(DateTime asOf, ReceivableCutoffBasis basis = ReceivableCutoffBasis.Posted)
    {
        var response = await _client.GetAuthAsync(
            $"/report/cheques/open-aging?asOfDate={asOf:yyyy-MM-dd}&cutoffBasis={basis}&page=1&pageSize=50", ManagerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseOpenChequeAgingJson>();
    }

    public async Task<ResponseBankReceiptJson> ReceiptAsync(Guid receiptId)
    {
        var response = await _client.GetAuthAsync($"/bank-receipt/{receiptId}", ManagerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseBankReceiptJson>();
    }

    public async Task<decimal> FiadoBalanceAsync(Guid clientId, DateTime asOf)
    {
        var response = await _client.GetAuthAsync($"/report/fiado/balance?clientId={clientId}&asOfDate={asOf:yyyy-MM-dd}", ManagerToken);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadContentAsync<ResponseFiadoBalanceJson>()).TotalOutstanding;
    }
}
