using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;

namespace WebApi.Test.Sessions;

/// <summary>
/// M9.6 Phases 6, 7 and 9 on a branch seeded by the real bootstrap (system types, repayment methods, expenses): a
/// cashbox created through the API (so it gets its paired Tab by default), a cofre, two granted Members, the recording
/// Manager and a second Manager who approves, a client, and an opening count of R$ 1.000,00 in Dinheiro.
/// </summary>
internal sealed class FinancialDayScenario
{
    private readonly ServerWebApplicationFactory _factory;
    private readonly HttpClient _client;

    private FinancialDayScenario(ServerWebApplicationFactory factory, HttpClient client)
    {
        _factory = factory;
        _client = client;
    }

    public Branch Branch { get; private init; } = null!;
    public SessionActor Manager { get; private init; } = null!;
    public SessionActor Approver { get; private init; } = null!;
    public SessionActor MemberA { get; private init; } = null!;
    public SessionActor MemberB { get; private init; } = null!;
    public Guid CashboxId { get; private init; }
    public Guid TabId { get; private init; }
    public Guid SafeId { get; private init; }
    public Guid ClientId { get; private init; }
    public IReadOnlyDictionary<string, Product> Products { get; private init; } = null!;
    public IReadOnlyDictionary<TransactionTypeSystemRole, TransactionType> SystemTypes { get; private init; } = null!;
    public IReadOnlyDictionary<string, TransactionType> SeededTypes { get; private init; } = null!;
    public DateTime Today { get; private init; } = SessionFlow.LocalToday();

    public Product Cash => Products["Dinheiro"];
    public Product Stock => Products["Raspadinha"];
    public Product CashVariance => Products[CashVarianceProductResolver.CashVarianceProductName];
    public TransactionType FiadoSaleType => SeededTypes["saidas.cliente"];
    public TransactionType CashMethod => SeededTypes["entradas.metodo-dinheiro"];
    public TransactionType PixMethod => SeededTypes["entradas.metodo-pix"];

    public static async Task<FinancialDayScenario> CreateAsync(
        ServerWebApplicationFactory factory,
        HttpClient client,
        string label,
        decimal openingCash = 1_000m,
        decimal openingStock = 0m,
        bool grantSafeToMemberA = true,
        DateTime? businessDate = null)
    {
        var branch = await factory.SeedConfiguredBranchAsync($"{label} {Guid.NewGuid():N}");
        var manager = await ActorAsync(factory, branch, Role.Manager, withOperator: false);
        var approver = await ActorAsync(factory, branch, Role.Manager, withOperator: false);
        var memberA = await ActorAsync(factory, branch, Role.Member, withOperator: true, "A");
        var memberB = await ActorAsync(factory, branch, Role.Member, withOperator: true, "B");

        var cashboxResponse = await client.PostAuthAsync(
            "/account/terminal", new RequestCreateTerminalAccountJson { Name = $"Caixa {label}" }, manager.Token);
        cashboxResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await cashboxResponse.Content.ReadAsStringAsync());
        var cashbox = await cashboxResponse.ReadContentAsync<ResponseCreateAccountJson>();
        cashbox.TabAccountId.ShouldNotBeNull();

        var safeResponse = await client.PostAuthAsync(
            "/account/safe", new RequestCreateSafeAccountJson { Name = $"Cofre {label}" }, manager.Token);
        safeResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await safeResponse.Content.ReadAsStringAsync());
        var safe = await safeResponse.ReadContentAsync<ResponseCreateAccountJson>();

        foreach (var member in new[] { memberA, memberB })
        {
            await factory.SeedOperatorAccountAsync(member.Operator!.Id, cashbox.Id, isPrimary: true);
            await factory.SeedOperatorAccountAsync(member.Operator!.Id, cashbox.TabAccountId!.Value);
        }
        if (grantSafeToMemberA)
            await factory.SeedOperatorAccountAsync(memberA.Operator!.Id, safe.Id);

        var clientRow = await factory.SeedClientAsync(branch.Id, "Dona Maria");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var products = await db.Products.AsNoTracking().Where(p => p.BranchId == branch.Id && p.Active).ToListAsync();
        var types = await db.TransactionTypes.AsNoTracking().Include(t => t.Category)
            .Where(t => t.Category.BranchId == branch.Id).ToListAsync();

        var scenario = new FinancialDayScenario(factory, client)
        {
            Today = businessDate ?? SessionFlow.LocalToday(),
            Branch = branch,
            Manager = manager,
            Approver = approver,
            MemberA = memberA,
            MemberB = memberB,
            CashboxId = cashbox.Id,
            TabId = cashbox.TabAccountId!.Value,
            SafeId = safe.Id,
            ClientId = clientRow.Id,
            Products = products.ToDictionary(p => p.Name),
            SystemTypes = types.Where(t => t.SystemRole != null).ToDictionary(t => t.SystemRole!.Value),
            SeededTypes = types.Where(t => t.SeedKey != null).ToDictionary(t => t.SeedKey!)
        };

        var opening = new List<RequestSessionOpeningItemJson>
        {
            new() { ProductId = scenario.Cash.Id, Value = openingCash },
            new() { ProductId = scenario.Stock.Id, Value = openingStock }
        };
        var record = await client.PostAuthAsync(
            "/session/opening-count",
            new RequestRecordOpeningCountJson { AccountId = cashbox.Id, AsOfDate = scenario.Today, Items = opening },
            manager.Token);
        record.StatusCode.ShouldBe(HttpStatusCode.OK, await record.Content.ReadAsStringAsync());
        return scenario;
    }

    /// <summary>Member A acknowledges the proposed opening (Conferir abertura e iniciar) and holds custody.</summary>
    public async Task<Guid> StartAsync(SessionActor? actor = null)
    {
        var started = await _client.StartSessionAsync(CashboxId, (actor ?? MemberA).Token);
        return started.Session.Id;
    }

    public Task<HttpResponseMessage> SaveAsync(
        Guid sessionId,
        string token,
        uint version,
        decimal cash,
        decimal stock = 0m,
        IReadOnlyList<RequestSessionCloseFieldJson>? fields = null,
        string? notes = null)
    {
        return _client.PutAuthAsync(
            $"/session/{sessionId}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = version,
                Items =
                [
                    new RequestUpsertDailyCloseItemJson { ProductId = Cash.Id, Value = cash },
                    new RequestUpsertDailyCloseItemJson { ProductId = Stock.Id, Value = stock }
                ],
                Notes = notes,
                CloseFields = fields
            },
            token);
    }

    public async Task<ResponseDailyCloseJson> SaveOkAsync(
        Guid sessionId,
        decimal cash,
        decimal stock = 0m,
        IReadOnlyList<RequestSessionCloseFieldJson>? fields = null,
        SessionActor? actor = null)
    {
        var token = (actor ?? MemberA).Token;
        var current = await _client.GetSessionAsync(sessionId, token);
        var response = await SaveAsync(sessionId, token, current.Version, cash, stock, fields);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;
    }

    public static IReadOnlyList<RequestSessionCloseFieldJson> Fields(
        decimal? report = null,
        decimal? bolao = null,
        decimal? special = null)
    {
        var fields = new List<RequestSessionCloseFieldJson>();
        if (report is { } r) fields.Add(new RequestSessionCloseFieldJson { Field = SessionCloseField.TerminalReport, Value = r });
        if (bolao is { } b) fields.Add(new RequestSessionCloseFieldJson { Field = SessionCloseField.BolaoFee, Value = b });
        if (special is { } s) fields.Add(new RequestSessionCloseFieldJson { Field = SessionCloseField.SpecialFee, Value = s });
        return fields;
    }

    public RequestCreateTransactionJson FiadoSale(decimal value, bool saveAsDraft = false) => new()
    {
        Date = Today,
        Value = value,
        TransactionTypeId = FiadoSaleType.Id,
        AccountId = TabId,
        ClientId = ClientId,
        SaveAsDraft = saveAsDraft
    };

    public RequestReceiveFiadoPaymentJson Repayment(TransactionType method, decimal value) => new()
    {
        Date = Today,
        Value = value,
        TransactionTypeId = method.Id,
        AccountId = TabId,
        ClientId = ClientId
    };

    /// <param name="byManager">A Manager without an operator link records for the custodian (Member A).</param>
    public RequestSafeDepositJson Deposit(decimal value, Guid? safeId = null, bool saveAsDraft = false, bool byManager = false) => new()
    {
        Date = Today,
        Value = value,
        AccountId = CashboxId,
        SafeAccountId = safeId ?? SafeId,
        SaveAsDraft = saveAsDraft,
        RecordedByOperatorId = byManager ? MemberA.Operator!.Id : null
    };

    /// <summary>A debt from a dedicated origin (opening obligation, §6.19 O19): a Tab Out with no cashbox leg.</summary>
    public Task<Transaction> SeedOpeningDebtAsync(decimal value) =>
        _factory.SeedTransactionAsync(
            Branch.Id, TabId, FiadoSaleType.Id, FiadoSaleType.CategoryId, Direction.Out, MemberA.Operator!.Id, Manager.User.Id,
            date: Today.AddDays(-1), value: value, clientId: ClientId, dueDate: Today.AddDays(-1));

    public async Task<IReadOnlyList<Transaction>> RowsAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await db.Transactions.AsNoTracking().Where(t => t.BranchId == Branch.Id).OrderBy(t => t.CreatedAt).ToListAsync();
    }

    public async Task<T> QueryAsync<T>(Func<ServerDbContext, Task<T>> query)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<ServerDbContext>());
    }

    public async Task<decimal> OutstandingAsync()
    {
        var response = await _client.GetAuthAsync($"/client/{ClientId}/fiado-statement", Manager.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadContentAsync<ResponseClientFiadoStatementJson>()).OutstandingTotal;
    }

    private static async Task<SessionActor> ActorAsync(
        ServerWebApplicationFactory factory,
        Branch branch,
        Role role,
        bool withOperator,
        string suffix = "")
    {
        var user = await factory.SeedUserAsync();
        var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, role);
        var op = withOperator ? await factory.SeedOperatorAsync(branch.Id, $"Operador {suffix}", user.Id) : null;
        return new SessionActor(user, membership, op, factory.IssueBranchToken(membership));
    }
}
