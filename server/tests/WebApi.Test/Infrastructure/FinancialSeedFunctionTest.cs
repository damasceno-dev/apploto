using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Sessions;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// M9.6 Phases 6, 7 and 9 seed function on real PostgreSQL, for a branch that predates the financial catalog (old
/// ordinary Tarifa Bolão/Operador/Depósito Dinheiro types and fee/report rows): conversion only of reviewed
/// identities, adoption of a single ordinary row per open session and field, ambiguity kept explicit, locked history
/// untouched, same-name custom types never adopted, and a rerun that changes nothing. The function only touches the
/// branches its map names, so it runs on the shared database.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class FinancialSeedFunctionTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ReviewedMap_ShouldConvertSeedAndAdopt_ExactlyOnce()
    {
        var legacy = await LegacyBranch.SeedAsync(factory, "SeedFunction");
        var map = legacy.Map();

        var outcomes = await Apply(map);

        outcomes.ShouldContain(("type:receita.tarifa-bolao", "converted"));
        outcomes.ShouldContain(("type:entradas.relatorio-terminal", "converted"));
        outcomes.ShouldContain(("type:saidas.deposito-dinheiro", "converted"));
        outcomes.ShouldContain(("type:receita.tarifa-especial", "created"));
        outcomes.ShouldContain(("type:saidas.relatorio-terminal-negativo", "created"));
        outcomes.ShouldContain(("type:sistema.cofre-deposito", "created"));
        outcomes.ShouldContain(("type:entradas.metodo-dinheiro", "created"));
        outcomes.ShouldContain(("type:entradas.metodo-pix", "created"));
        // A same-name custom expense is never adopted: that one seed is skipped and reported.
        outcomes.ShouldContain(("type:administrativas.agua", "skipped-name-collision"));
        outcomes.Count(o => o.Item.StartsWith("type:administrativas.") || o.Item.StartsWith("type:pessoal.")
            || o.Item.StartsWith("type:financeiras.")).ShouldBe(32);
        outcomes.ShouldContain(($"session:{legacy.OpenSession.Id}:field:1", "adopted"));
        outcomes.ShouldContain(($"session:{legacy.OpenSession.Id}:field:2", "adopted"));
        outcomes.ShouldContain(($"session:{legacy.AmbiguousSession.Id}:field:2", "ambiguous"));
        outcomes.ShouldNotContain(o => o.Item.StartsWith($"session:{legacy.LockedSession.Id}"));

        var types = await Types(legacy.Branch.Id);
        var tarifa = types.Single(t => t.Id == legacy.Tarifa.Id);
        (tarifa.SystemRole, tarifa.MonthlyFinancialRole, tarifa.Name, tarifa.SeedKey)
            .ShouldBe((TransactionTypeSystemRole.BolaoFee, MonthlyFinancialRole.BolaoServiceFee, "Tarifa Bolão", "receita.tarifa-bolao"));
        var report = types.Single(t => t.Id == legacy.Operador.Id);
        (report.SystemRole, report.Name).ShouldBe((TransactionTypeSystemRole.TerminalReportIn, "Relatório do terminal"));
        types.Single(t => t.Id == legacy.Deposit.Id).SystemRole.ShouldBe(TransactionTypeSystemRole.SafeDepositOut);
        types.Single(t => t.SystemRole == TransactionTypeSystemRole.SpecialFee).MonthlyFinancialRole
            .ShouldBe(MonthlyFinancialRole.SpecialDrawServiceFee);
        types.Single(t => t.Id == legacy.CustomAgua.Id).SeedKey.ShouldBeNull();
        types.Count(t => t.Name == "Água" && t.Active).ShouldBe(1);
        types.Single(t => t.SeedKey == "entradas.metodo-dinheiro").FiadoPaymentKind.ShouldBe(FiadoPaymentKind.CountedCash);
        types.Single(t => t.SeedKey == "entradas.metodo-pix").FiadoPaymentKind.ShouldBe(FiadoPaymentKind.NonCash);

        // The open session's single rows became its field effects, keeping their ids, amounts and authorship.
        var slots = await Slots(legacy.OpenSession.Id);
        slots.Select(s => (s.Field, s.Value, s.Revision, s.CurrentTransactionId)).ShouldBe([
            (SessionCloseField.TerminalReport, 350m, 1, (Guid?)legacy.OpenReport.Id),
            (SessionCloseField.BolaoFee, 120m, 1, (Guid?)legacy.OpenFee.Id)
        ]);
        var adopted = await Row(legacy.OpenFee.Id);
        (adopted.Value, adopted.Status, adopted.CreatedByUserId, adopted.GenerationKind)
            .ShouldBe((120m, TransactionStatus.Active, legacy.UserId, (TransactionGenerationKind?)TransactionGenerationKind.SessionBolaoFee));
        (await Revisions(slots[1].Id)).Single().Origin.ShouldBe(SessionFieldEffectOrigin.MigratedLegacyRow);

        // Two rows for one field stay exactly as they were, with an explicit issue; the locked session is untouched.
        (await Slots(legacy.AmbiguousSession.Id)).ShouldBeEmpty();
        (await Row(legacy.AmbiguousFees[0].Id)).GenerationKind.ShouldBeNull();
        (await Row(legacy.AmbiguousFees[1].Id)).GenerationKind.ShouldBeNull();
        var issue = await factory.ReadDb(db => db.CustodyMappingIssues.AsNoTracking()
            .SingleAsync(i => i.SourceId == legacy.AmbiguousSession.Id));
        (issue.IssueCode, issue.Status).ShouldBe((CustodyMappingIssueCode.AmbiguousSessionFieldRows, CustodyMappingIssueStatus.Unresolved));
        (await Slots(legacy.LockedSession.Id)).ShouldBeEmpty();
        var lockedRow = await Row(legacy.LockedFee.Id);
        (lockedRow.GenerationKind, lockedRow.Version).ShouldBe(((TransactionGenerationKind?)null, legacy.LockedFee.Version));

        // Rerunning the same map changes nothing: every row reports present and no version moves.
        var snapshot = await Snapshot(legacy.Branch.Id);
        var rerun = await Apply(map);
        rerun.ShouldNotContain(o => o.Outcome == "created" || o.Outcome == "converted" || o.Outcome == "adopted" || o.Outcome == "ambiguous");
        (await Snapshot(legacy.Branch.Id)).ShouldBe(snapshot);
    }

    [Fact]
    public async Task AdoptedAndAmbiguousFields_ShouldBehaveThroughTheApi()
    {
        var legacy = await LegacyBranch.SeedAsync(factory, "SeedFunctionApi");
        await Apply(legacy.Map());

        // The adopted effect is replaced like any field effect: cancelled, with its history continuing at revision 2.
        var open = await _client.GetSessionAsync(legacy.OpenSession.Id, legacy.Token);
        var save = await _client.PutAuthAsync(
            $"/session/{legacy.OpenSession.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = open.Version,
                Items = [new() { ProductId = legacy.Cash.Id, Value = 1_000m }],
                CloseFields = [new() { Field = SessionCloseField.BolaoFee, Value = 150m }]
            },
            legacy.Token);
        save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
        (await Row(legacy.OpenFee.Id)).Status.ShouldBe(TransactionStatus.Cancelled);
        (await Slots(legacy.OpenSession.Id)).Single(s => s.Field == SessionCloseField.BolaoFee).Revision.ShouldBe(2);

        // The ambiguous session refuses that field (never a third effect beside two legacy rows) but not the others.
        var ambiguous = await _client.GetSessionAsync(legacy.AmbiguousSession.Id, legacy.Token);
        var refused = await _client.PutAuthAsync(
            $"/session/{legacy.AmbiguousSession.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = ambiguous.Version,
                Items = [new() { ProductId = legacy.Cash.Id, Value = 1_000m }],
                CloseFields = [new() { Field = SessionCloseField.BolaoFee, Value = 10m }]
            },
            legacy.Token);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict, await refused.Content.ReadAsStringAsync());
        (await refused.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.SESSION_CLOSE_FIELD_LEGACY_ROWS_UNRESOLVED);
        var other = await _client.PutAuthAsync(
            $"/session/{legacy.AmbiguousSession.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = ambiguous.Version,
                Items = [new() { ProductId = legacy.Cash.Id, Value = 1_000m }],
                CloseFields = [new() { Field = SessionCloseField.SpecialFee, Value = 10m }]
            },
            legacy.Token);
        other.StatusCode.ShouldBe(HttpStatusCode.OK, await other.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InvalidMaps_ShouldRaiseAndWriteNothing()
    {
        var legacy = await LegacyBranch.SeedAsync(factory, "SeedFunctionInvalid");
        var before = await Snapshot(legacy.Branch.Id);

        var missingKey = legacy.Map(omit: "safeDepositTypeId");
        (await Should.ThrowAsync<PostgresException>(() => Apply(missingKey))).MessageText.ShouldContain("M96_PREFLIGHT");
        var wrongCategory = legacy.Map(overrides: ("bolaoFeeTypeId", legacy.Operador.Id));
        (await Should.ThrowAsync<PostgresException>(() => Apply(wrongCategory))).MessageText.ShouldContain("M96_PREFLIGHT");
        // Null for an existing default-named type means "create one", which would adopt a same-name type: refused.
        var unmappedTarifa = legacy.Map(overrides: ("bolaoFeeTypeId", null));
        (await Should.ThrowAsync<PostgresException>(() => Apply(unmappedTarifa))).MessageText.ShouldContain("not adopted");

        (await Snapshot(legacy.Branch.Id)).ShouldBe(before);
    }

    private async Task<List<(string Item, string Outcome)>> Apply(string map)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using var command = new NpgsqlCommand("SELECT item, outcome FROM loto_m96_apply_financial_seeds(@map::jsonb)", connection, transaction);
        command.Parameters.AddWithValue("map", map);
        var outcomes = new List<(string, string)>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync())
                outcomes.Add((reader.GetString(0), reader.GetString(1)));
        await transaction.CommitAsync();
        return outcomes;
    }

    private Task<List<TransactionType>> Types(Guid branchId) =>
        factory.ReadDb(db => db.TransactionTypes.AsNoTracking().Where(t => t.Category.BranchId == branchId).ToListAsync());

    private Task<List<SessionFieldEffect>> Slots(Guid sessionId) =>
        factory.ReadDb(db => db.SessionFieldEffects.AsNoTracking().Where(s => s.SessionId == sessionId).OrderBy(s => s.Field).ToListAsync());

    private Task<List<SessionFieldEffectRevision>> Revisions(Guid slotId) =>
        factory.ReadDb(db => db.SessionFieldEffectRevisions.AsNoTracking().Where(r => r.SessionFieldEffectId == slotId).ToListAsync());

    private Task<Transaction> Row(Guid id) =>
        factory.ReadDb(db => db.Transactions.AsNoTracking().SingleAsync(t => t.Id == id));

    private Task<string> Snapshot(Guid branchId) => factory.ReadDb(async db => JsonSerializer.Serialize(new
    {
        Types = await db.TransactionTypes.AsNoTracking().Where(t => t.Category.BranchId == branchId).OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Name, t.SystemRole, t.SeedKey, t.MonthlyFinancialRole, t.Active }).ToListAsync(),
        Categories = await db.Categories.AsNoTracking().Where(c => c.BranchId == branchId).OrderBy(c => c.Id)
            .Select(c => new { c.Id, c.Name }).ToListAsync(),
        Rows = await db.Transactions.AsNoTracking().Where(t => t.BranchId == branchId).OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Version, t.GenerationKind, t.Status }).ToListAsync(),
        Slots = await db.SessionFieldEffects.AsNoTracking().Where(s => s.BranchId == branchId).CountAsync(),
        Issues = await db.CustodyMappingIssues.AsNoTracking().Where(i => i.BranchId == branchId).CountAsync()
    }));

    /// <summary>A branch as it looked before M9.6 Phase 6: the M9.5 catalog, ordinary fee/report rows on sessions.</summary>
    private sealed class LegacyBranch
    {
        public required Branch Branch { get; init; }
        public required string Token { get; init; }
        public required Guid UserId { get; init; }
        public required Product Cash { get; init; }
        public required Dictionary<string, Category> Categories { get; init; }
        public required TransactionType Tarifa { get; init; }
        public required TransactionType Operador { get; init; }
        public required TransactionType Deposit { get; init; }
        public required TransactionType CustomAgua { get; init; }
        public required DailyClose OpenSession { get; init; }
        public required DailyClose AmbiguousSession { get; init; }
        public required DailyClose LockedSession { get; init; }
        public required Transaction OpenFee { get; init; }
        public required Transaction OpenReport { get; init; }
        public required Transaction[] AmbiguousFees { get; init; }
        public required Transaction LockedFee { get; init; }

        public static async Task<LegacyBranch> SeedAsync(ServerWebApplicationFactory factory, string label)
        {
            var (user, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
            var op = await factory.SeedOperatorAsync(branch.Id, userId: user.Id);
            var cashbox = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal, "Caixa legado");
            await factory.EnsureCashVarianceProductAsync(branch.Id);
            var cash = await factory.SeedProductAsync(branch.Id, "Dinheiro", displayOrder: 1);
            var categories = new Dictionary<string, Category>
            {
                ["Receita"] = await factory.SeedCategoryAsync(branch.Id, "Receita", Direction.In),
                ["Entradas"] = await factory.SeedCategoryAsync(branch.Id, "Entradas", Direction.In),
                ["Saídas"] = await factory.SeedCategoryAsync(branch.Id, "Saídas", Direction.Out),
                ["Administrativas"] = await factory.SeedCategoryAsync(branch.Id, "Despesas Administrativas", Direction.Out),
                ["Pessoal"] = await factory.SeedCategoryAsync(branch.Id, "Despesas Pessoal", Direction.Out),
                ["Financeiras"] = await factory.SeedCategoryAsync(branch.Id, "Despesas Financeiras", Direction.Out)
            };
            var tarifa = await factory.SeedTransactionTypeAsync(categories["Receita"].Id, "Tarifa Bolão",
                monthlyFinancialRole: MonthlyFinancialRole.BolaoServiceFee);
            var operador = await factory.SeedTransactionTypeAsync(categories["Entradas"].Id, "Operador");
            var deposit = await factory.SeedTransactionTypeAsync(categories["Saídas"].Id, "Depósito Dinheiro",
                SettlementRule.NextCalendarDay);
            var agua = await factory.SeedTransactionTypeAsync(categories["Administrativas"].Id, "Água");

            var today = SessionFlow.LocalToday();
            var lockDate = today.AddDays(-10);
            await factory.ExecuteSqlAsync($"""
                INSERT INTO "Settings" ("Id", "BranchId", "LockDate", "DailyTargetHours", "LunchDeductionOver6H",
                    "LunchDeductionOver4H", "Active", "CreatedAt")
                VALUES (gen_random_uuid(), '{branch.Id}', '{lockDate:yyyy-MM-dd}', 7.33, 1, 0.25, true, now())
                """);
            var locked = await factory.SeedDailyCloseAsync(branch.Id, cashbox.Id, today.AddDays(-12), DailyCloseStatus.Approved,
                custodianOperatorId: op.Id, custodyState: SessionCustodyState.Finished);
            var ambiguous = await factory.SeedDailyCloseAsync(branch.Id, cashbox.Id, today.AddDays(-1),
                custodianOperatorId: op.Id, custodyState: SessionCustodyState.Finished);
            var open = await factory.SeedDailyCloseAsync(branch.Id, cashbox.Id, today, custodianOperatorId: op.Id);

            Task<Transaction> Row(TransactionType type, Category category, DailyClose session, decimal value) =>
                factory.SeedTransactionAsync(branch.Id, cashbox.Id, type.Id, category.Id, Direction.In, op.Id, user.Id,
                    date: session.Date, value: value, sessionId: session.Id);

            return new LegacyBranch
            {
                Branch = branch,
                Token = token,
                UserId = user.Id,
                Cash = cash,
                Categories = categories,
                Tarifa = tarifa,
                Operador = operador,
                Deposit = deposit,
                CustomAgua = agua,
                OpenSession = open,
                AmbiguousSession = ambiguous,
                LockedSession = locked,
                OpenFee = await Row(tarifa, categories["Receita"], open, 120m),
                OpenReport = await Row(operador, categories["Entradas"], open, 350m),
                AmbiguousFees =
                [
                    await Row(tarifa, categories["Receita"], ambiguous, 60m),
                    await Row(tarifa, categories["Receita"], ambiguous, 40m)
                ],
                LockedFee = await Row(tarifa, categories["Receita"], locked, 90m)
            };
        }

        public string Map(string? omit = null, (string Key, Guid? Value)? overrides = null)
        {
            var entry = new Dictionary<string, object?>
            {
                ["branchId"] = Branch.Id,
                ["receitaCategoryId"] = Categories["Receita"].Id,
                ["entradasCategoryId"] = Categories["Entradas"].Id,
                ["saidasCategoryId"] = Categories["Saídas"].Id,
                ["despesasAdministrativasCategoryId"] = Categories["Administrativas"].Id,
                ["despesasPessoalCategoryId"] = Categories["Pessoal"].Id,
                ["despesasFinanceirasCategoryId"] = Categories["Financeiras"].Id,
                ["bolaoFeeTypeId"] = Tarifa.Id,
                ["terminalReportTypeId"] = Operador.Id,
                ["safeDepositTypeId"] = Deposit.Id
            };
            if (omit is not null)
                entry.Remove(omit);
            if (overrides is { } o)
                entry[o.Key] = o.Value;
            return JsonSerializer.Serialize(new[] { entry });
        }
    }
}
