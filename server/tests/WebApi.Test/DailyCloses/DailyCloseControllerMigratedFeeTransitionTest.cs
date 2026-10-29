using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Infrastructure;
using Shouldly;
using Testcontainers.PostgreSql;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerMigratedFeeTransitionTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task FirstSessionAfterRealUpgrade_ShouldQuarantineLegacyHistory_WithoutRewritingIt()
    {
        await using var postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithCreateParameterModifier(parameters =>
            {
                foreach (var bindings in parameters.HostConfig!.PortBindings!.Values)
                    foreach (var binding in bindings) binding.HostIP = "127.0.0.1";
            })
            .WithDatabase("loto_m95_transition").WithUsername("postgres").WithPassword("postgres").Build();
        await postgres.StartAsync();
        var options = new DbContextOptionsBuilder<ServerDbContext>().UseNpgsql(postgres.GetConnectionString()).Options;
        await using var db = new ServerDbContext(options);
        await db.GetService<IMigrator>().MigrateAsync("20261018165353_M8OperatedOnboardingAndGoogleIdentity");
        var branch = new Branch { Name = "Transition" };
        var user = new User { Name = "Reviewer", Email = "reviewer@example.invalid", Password = "unused" };
        var membership = new BranchUser { BranchId = branch.Id, UserId = user.Id, Role = Role.Admin };
        var cash = new Product { BranchId = branch.Id, Name = "Dinheiro", DisplayOrder = 5 };
        var fee = new Product { BranchId = branch.Id, Name = "Legacy fee", DisplayOrder = 6 };
        var variance = new Product { BranchId = branch.Id, Name = CashVarianceProductResolver.CashVarianceProductName, DisplayOrder = 8 };
        var date = new DateTime(2026, 9, 15);
        var category = new Category { BranchId = branch.Id, Name = "Vendas legado", DefaultDirection = Direction.In };
        db.AddRange(branch, user, membership, cash, fee, variance, category,
            new Setting { BranchId = branch.Id, LockDate = date.AddDays(-1) });
        await db.SaveChangesAsync();

        // Rows at the earlier schema are written with that schema's columns (the current entities carry session ones).
        var accountId = Guid.NewGuid();
        var priorId = Guid.NewGuid();
        var priorFeeId = Guid.NewGuid();
        var typeId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var orphanRowId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Accounts" ("Id", "Type", "Name", "BranchId", "CreatedAt", "Active")
            VALUES ({accountId}, 0, 'Terminal', {branch.Id}, now(), true);
            INSERT INTO "DailyCloses" ("Id", "Date", "Status", "AccountId", "OpenedByUserId", "RecordedByUserId",
                "ItemsFirstRecordedAt", "BranchId", "ReviewHistoryComplete", "CreatedAt", "Active")
            VALUES ({priorId}, {DateOnly.FromDateTime(date.AddDays(-1))}, 2, {accountId}, {user.Id}, {user.Id}, now(), {branch.Id}, true, now(), true);
            INSERT INTO "DailyCloseItems" ("Id", "Value", "DailyCloseId", "ProductId", "CreatedAt", "Active")
            VALUES ({priorFeeId}, 100, {priorId}, {fee.Id}, now(), true),
                   ({Guid.NewGuid()}, 1000, {priorId}, {cash.Id}, now(), true);
            INSERT INTO "TransactionTypes" ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId",
                "CreatedAt", "Active")
            VALUES ({typeId}, 'Venda', 0, false, {category.Id}, now(), true);
            INSERT INTO "Operators" ("Id", "Name", "BranchId", "CreatedAt", "Active")
            VALUES ({operatorId}, 'Operador legado', {branch.Id}, now(), true);
            INSERT INTO "Transactions" ("Id", "Date", "Value", "TransactionTypeId", "CategoryId", "Direction", "AccountId",
                "DueDate", "RecordedByOperatorId", "CreatedByUserId", "Status", "BranchId", "CreatedAt", "Active")
            VALUES ({orphanRowId}, {DateOnly.FromDateTime(date)}, 30, {typeId}, {category.Id}, 0, {accountId},
                {DateOnly.FromDateTime(date)}, {operatorId}, {user.Id}, 0, {branch.Id}, now(), true);
            """);
        await db.Database.OpenConnectionAsync();
        var map = JsonSerializer.Serialize(new[] { new { branchId = branch.Id, productId = fee.Id,
            receitaCategoryId = (Guid?)null, entradasCategoryId = (Guid?)null } });
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('loto.m95_seed_map', {map}, false)");
        await db.Database.MigrateAsync();

        // The upgraded legacy close is Unresolved history: identity and amounts kept, no invented custody.
        db.ChangeTracker.Clear();
        var prior = await db.DailyCloses.SingleAsync(c => c.Id == priorId);
        prior.MappingState.ShouldBe(SessionMappingState.Unresolved);
        prior.Origin.ShouldBe(SessionOrigin.Migrated);
        prior.Status.ShouldBe(DailyCloseStatus.Approved);
        prior.CustodyState.ShouldBeNull();
        prior.CustodianOperatorId.ShouldBeNull();
        prior.Sequence.ShouldBeNull();
        prior.CustodyHistoryComplete.ShouldBeFalse();
        (await db.CustodyMappingIssues.CountAsync(issue =>
            issue.SourceId == priorId && issue.IssueCode == CustodyMappingIssueCode.LegacySessionUnmapped)).ShouldBe(1);
        (await db.DailyCloseItems.SingleAsync(i => i.Id == priorFeeId)).Value.ShouldBe(100m);
        // A cashbox row without a close stays unsessioned, unresolved orphan activity.
        (await db.Transactions.AsNoTracking().Where(t => t.Id == orphanRowId).Select(t => t.SessionId).SingleAsync()).ShouldBeNull();
        (await db.CustodyMappingIssues.CountAsync(issue =>
            issue.SourceId == orphanRowId && issue.IssueCode == CustodyMappingIssueCode.OrphanCashboxActivity)).ShouldBe(1);
        (await db.Products.SingleAsync(p => p.Id == fee.Id)).Active.ShouldBeFalse();

        // Boot the production HTTP application against this upgraded disposable database.
        // Leave the shared factory's environment and database untouched.
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<ServerDbContext>();
            services.RemoveAll<DbContextOptions<ServerDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ServerDbContext>>();
            services.AddDbContext<ServerDbContext>(o => o.UseNpgsql(postgres.GetConnectionString()));
        }));
        using var scope = app.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<ITokenServices>().GenerateBranchToken(membership);
        using var client = app.CreateClient();

        // History stays readable…
        var priorResponse = await client.GetAuthAsync($"/session/{priorId}/review", token);
        priorResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await priorResponse.Content.ReadAsStringAsync());
        var priorReview = await priorResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        priorReview.Custody.MappingState.ShouldBe(SessionMappingState.Unresolved);
        priorReview.Items.ShouldContain(i => i.ProductId == fee.Id && i.ClosingValue == 100m);

        // …but a cashbox with unresolved history cannot silently start fresh over it.
        var proposalResponse = await client.GetAuthAsync($"/session/opening-proposal?accountId={accountId}", token);
        var proposal = await proposalResponse.ReadContentAsync<ResponseSessionOpeningProposalJson>();
        proposal.HasUnresolvedHistory.ShouldBeTrue();
        proposal.CanStart.ShouldBeFalse();
        var reopen = await client.PostAuthAsync($"/session/{priorId}/reopen", token);
        reopen.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await reopen.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldHaveSingleItem().ShouldBe(server.Exceptions.ResourcesErrorMessages.SESSION_UNRESOLVED_NOT_WRITABLE);
    }
}
