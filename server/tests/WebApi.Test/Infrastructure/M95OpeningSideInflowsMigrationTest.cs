using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using Xunit;

namespace WebApi.Test.Infrastructure;

public sealed class M95OpeningSideInflowsMigrationTest : IAsyncLifetime
{
    private const string Previous = "20261018165353_M8OperatedOnboardingAndGoogleIdentity";
    private const string M95Migration = "20261020104851_M95OpeningSideInflows";
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithCreateParameterModifier(parameters =>
        {
            foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
        })
        .WithDatabase("loto_m95_upgrade").WithUsername("postgres").WithPassword("postgres").Build();

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();
    private ServerDbContext Context() => new(new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    [Fact]
    public async Task FreshDatabase_AndRepeatedMigrate_ShouldHaveNoPendingModelOrDuplicateMigration()
    {
        await using var db = Context();
        await db.Database.MigrateAsync();
        await db.Database.MigrateAsync();
        db.Database.HasPendingModelChanges().ShouldBeFalse();
        (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();
        (await db.TransactionTypes.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Upgrade_ExplicitRenamedAndRetiredIdentities_ShouldPreserveCustomRows_AndBeIdempotent()
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        var first = new Branch { Name = "First" };
        var second = new Branch { Name = "Retired branch", Active = false };
        var fee = new Product { BranchId = first.Id, Name = "Renamed fee", DisplayOrder = 23 };
        var retiredFee = new Product { BranchId = second.Id, Name = "Old fee", Active = false };
        var custom = new Product { BranchId = first.Id, Name = "Tarifa Bolão" };
        var receita = new Category { BranchId = first.Id, Name = "Renamed income", DefaultDirection = Direction.In };
        db.AddRange(first, second, fee, retiredFee, custom, receita);
        await db.SaveChangesAsync();
        var retiredVersion = await ProductVersion(db, retiredFee.Id);
        await SetMap(db, new[] { Map(first.Id, fee.Id, receita.Id), Map(second.Id, retiredFee.Id) });
        await db.Database.MigrateAsync();
        db.ChangeTracker.Clear();
        (await db.Products.SingleAsync(p => p.Id == fee.Id)).Active.ShouldBeFalse();
        (await db.Products.SingleAsync(p => p.Id == custom.Id)).Active.ShouldBeTrue();
        (await ProductVersion(db, retiredFee.Id)).ShouldBe(retiredVersion);
        var types = await db.TransactionTypes.Include(t => t.Category).ToListAsync();
        types.Count.ShouldBe(4);
        types.ShouldAllBe(t => t.SettlementRule == SettlementRule.SameDay && !t.RequiresTabAccountAndClient
            && t.SystemRole == null && t.FiadoPaymentKind == null && t.Category.DefaultDirection == Direction.In);
        types.Count(t => t.Category.BranchId == first.Id).ShouldBe(2);
        types.Count(t => t.Category.BranchId == second.Id).ShouldBe(2);
        types.Single(t => t.Name == "Tarifa Bolão" && t.Category.BranchId == first.Id).CategoryId.ShouldBe(receita.Id);
        var ids = types.Select(t => t.Id).Order().ToArray();
        await db.Database.MigrateAsync();
        (await db.TransactionTypes.Select(t => t.Id).OrderBy(id => id).ToArrayAsync()).ShouldBe(ids);
    }

    [Theory]
    [InlineData("cross-branch-product")]
    [InlineData("system-category")]
    [InlineData("variance-product")]
    [InlineData("missing")]
    [InlineData("cross-branch")]
    [InlineData("out")]
    [InlineData("retired-category")]
    [InlineData("custom-type")]
    [InlineData("retired-type")]
    [InlineData("other-category-type")]
    [InlineData("retired-other-category-type")]
    public async Task AmbiguousOrInvalidMap_ShouldFailAtomically_WithoutHijackingCustomObjects(string scenario)
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        var branch = new Branch { Name = "Review required" };
        var other = new Branch { Name = "Other" };
        var product = new Product { BranchId = scenario == "cross-branch-product" ? other.Id : branch.Id,
            Name = scenario == "variance-product" ? "Diferença Caixa" : "Tarifa Bolão" };
        var category = new Category
        {
            BranchId = scenario == "cross-branch" ? other.Id : branch.Id,
            Name = "Receita",
            DefaultDirection = scenario == "out" ? Direction.Out : Direction.In,
            Active = scenario != "retired-category"
        };
        db.AddRange(branch, other, product, category);
        var otherCategoryCollision = scenario is "other-category-type" or "retired-other-category-type";
        var collisionCategory = otherCategoryCollision
            ? new Category { BranchId = branch.Id, Name = "Custom income", DefaultDirection = Direction.In,
                Active = scenario != "retired-other-category-type" }
            : category;
        if (otherCategoryCollision) db.Add(collisionCategory);
        await db.SaveChangesAsync();
        if (scenario == "system-category")
            await InsertPriorSchemaType(db, category.Id, "System counterpart", SettlementRule.SameDay, false, true,
                TransactionTypeSystemRole.FiadoCashRepaymentCounterpart);
        if (scenario is "custom-type" or "retired-type" || otherCategoryCollision)
            await InsertPriorSchemaType(db, collisionCategory.Id, "Tarifa Bolão", SettlementRule.TwoBusinessDays, true,
                scenario is not ("retired-type" or "retired-other-category-type"), null);
        var beforeVersion = await ProductVersion(db, product.Id);
        if (scenario != "missing")
            await SetMap(db, new[] { Map(branch.Id, product.Id, category.Id), Map(other.Id, null) });
        var error = await Should.ThrowAsync<PostgresException>(() => db.Database.MigrateAsync());
        error.MessageText.ShouldStartWith("M95_PREFLIGHT:");
        db.ChangeTracker.Clear();
        (await db.Products.SingleAsync(p => p.Id == product.Id)).Active.ShouldBeTrue();
        (await ProductVersion(db, product.Id)).ShouldBe(beforeVersion);
        (await db.TransactionTypes.CountAsync()).ShouldBe(scenario is "custom-type" or "retired-type" or "system-category" || otherCategoryCollision ? 1 : 0);
        if (scenario is "custom-type" or "retired-type" || otherCategoryCollision)
        {
            // Executable remediation: preserve the colliding object's identity and metadata,
            // and create the new inflow under an explicitly reviewed unused label.
            var customId = await db.TransactionTypes.Select(t => t.Id).SingleAsync();
            await SetMap(db, new object[]
            {
                new { branchId = branch.Id, productId = product.Id, receitaCategoryId = category.Id,
                    entradasCategoryId = (Guid?)null, receitaTypeName = "Taxa diária revisada" },
                Map(other.Id, null)
            });
            await db.Database.MigrateAsync();
            db.ChangeTracker.Clear();
            var custom = await db.TransactionTypes.SingleAsync(t => t.Id == customId);
            custom.SettlementRule.ShouldBe(SettlementRule.TwoBusinessDays);
            custom.RequiresTabAccountAndClient.ShouldBeTrue();
            custom.Active.ShouldBe(scenario is "custom-type" or "other-category-type");
            custom.CategoryId.ShouldBe(collisionCategory.Id);
            (await db.TransactionTypes.SingleAsync(t => t.Name == "Taxa diária revisada"))
                .SettlementRule.ShouldBe(SettlementRule.SameDay);
        }
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    public async Task HistoricalCount_LockedHistoryIsPreserved_UnlockedHistoryBlocksRetirement(bool locked, bool closeActive, bool itemActive)
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(Previous);
        var branch = new Branch { Name = "History" };
        var user = new User { Name = "Reviewer", Email = "reviewer@example.invalid", Password = "unused" };
        var fee = new Product { BranchId = branch.Id, Name = "Renamed fee" };
        var date = new DateTime(2026, 9, 10);
        db.AddRange(branch, user, fee, new Setting { BranchId = branch.Id, LockDate = locked ? date : DateTime.MinValue });
        await db.SaveChangesAsync();

        // Account and close rows are written with the pre-M9.6 columns; the current entities carry session ones.
        var accountId = Guid.NewGuid();
        var closeId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Accounts" ("Id", "Type", "Name", "BranchId", "CreatedAt", "Active")
            VALUES ({accountId}, 0, 'Terminal', {branch.Id}, now(), true);
            INSERT INTO "DailyCloses" ("Id", "Date", "Status", "AccountId", "OpenedByUserId", "RecordedByUserId",
                "ItemsFirstRecordedAt", "BranchId", "ReviewHistoryComplete", "CreatedAt", "Active")
            VALUES ({closeId}, {DateOnly.FromDateTime(date)}, 2, {accountId}, {user.Id}, {user.Id}, now(), {branch.Id}, true,
                now(), {closeActive});
            INSERT INTO "DailyCloseItems" ("Id", "Value", "DailyCloseId", "ProductId", "CreatedAt", "Active")
            VALUES ({itemId}, 120, {closeId}, {fee.Id}, now(), {itemActive});
            """);
        var version = await RowVersion(db, "DailyCloses", closeId);
        var itemVersion = await RowVersion(db, "DailyCloseItems", itemId);
        await SetMap(db, new[] { Map(branch.Id, fee.Id) });
        var succeeds = locked || !closeActive || !itemActive;
        var migrator = db.GetService<IMigrator>();
        if (succeeds) await migrator.MigrateAsync(M95Migration);
        else (await Should.ThrowAsync<PostgresException>(() => migrator.MigrateAsync(M95Migration))).MessageText.ShouldStartWith("M95_GAP_AA:");
        db.ChangeTracker.Clear();
        var saved = await db.DailyCloses.AsNoTracking()
            .Where(c => c.Id == closeId)
            .Select(c => new { c.Status, c.Active })
            .SingleAsync();
        (await RowVersion(db, "DailyCloses", closeId)).ShouldBe(version);
        saved.Status.ShouldBe(DailyCloseStatus.Approved);
        saved.Active.ShouldBe(closeActive);
        var savedItem = await db.DailyCloseItems.AsNoTracking().SingleAsync(i => i.Id == itemId);
        savedItem.DailyCloseId.ShouldBe(closeId);
        savedItem.Value.ShouldBe(120m);
        savedItem.Active.ShouldBe(itemActive);
        (await RowVersion(db, "DailyCloseItems", itemId)).ShouldBe(itemVersion);
        (await db.Products.SingleAsync(p => p.Id == fee.Id)).Active.ShouldBe(!succeeds);
    }

    private static object Map(Guid branchId, Guid? productId, Guid? receitaCategoryId = null) =>
        new { branchId, productId, receitaCategoryId, entradasCategoryId = (Guid?)null };

    private static async Task SetMap(ServerDbContext db, object mapping)
    {
        await db.Database.OpenConnectionAsync();
        var json = JsonSerializer.Serialize(mapping);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('loto.m95_seed_map', {json}, false)");
    }

    private static Task<long> RowVersion(ServerDbContext db, string table, Guid id) => db.Database
        .SqlQueryRaw<long>($"SELECT xmin::text::bigint AS \"Value\" FROM \"{table}\" WHERE \"Id\" = {{0}}", id).SingleAsync();

    private static Task<long> ProductVersion(ServerDbContext db, Guid id) => db.Database
        .SqlQuery<long>($"SELECT xmin::text::bigint AS \"Value\" FROM \"Products\" WHERE \"Id\" = {id}").SingleAsync();

    // Written with the pre-M11 column set: the current TransactionType entity has columns this schema predates.
    private static Task InsertPriorSchemaType(ServerDbContext db, Guid categoryId, string name, SettlementRule rule,
        bool requiresTab, bool active, TransactionTypeSystemRole? systemRole)
    {
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "TransactionTypes" ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "SystemRole",
                "FiadoPaymentKind", "CategoryId", "CreatedAt", "Active")
            VALUES ({Guid.NewGuid()}, {name}, {(short)rule}, {requiresTab}, {(short?)systemRole}, {(short?)null},
                {categoryId}, {DateTime.UtcNow}, {active})
            """);
    }
}
