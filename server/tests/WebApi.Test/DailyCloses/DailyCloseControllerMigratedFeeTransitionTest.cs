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
    public async Task FirstCloseAfterRealUpgrade_ShouldExposeHiddenPriorFeeLimitation_WithoutRewritingHistory()
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
        var account = new Account { BranchId = branch.Id, Name = "Terminal", Type = AccountType.Terminal };
        var cash = new Product { BranchId = branch.Id, Name = "Dinheiro", DisplayOrder = 5 };
        var fee = new Product { BranchId = branch.Id, Name = "Legacy fee", DisplayOrder = 6 };
        var variance = new Product { BranchId = branch.Id, Name = CashVarianceProductResolver.CashVarianceProductName, DisplayOrder = 8 };
        var date = new DateTime(2026, 9, 15);
        var prior = new DailyClose { BranchId = branch.Id, AccountId = account.Id, OpenedByUserId = user.Id,
            RecordedByUserId = user.Id, Date = date.AddDays(-1), ItemsFirstRecordedAt = DateTime.UtcNow,
            Status = DailyCloseStatus.Approved };
        var priorFee = new DailyCloseItem { DailyCloseId = prior.Id, ProductId = fee.Id, Value = 100m };
        db.AddRange(branch, user, membership, account, cash, fee, variance, prior, priorFee,
            new DailyCloseItem { DailyCloseId = prior.Id, ProductId = cash.Id, Value = 1_000m },
            new Setting { BranchId = branch.Id, LockDate = prior.Date });
        await db.SaveChangesAsync();
        var priorVersion = prior.Version;
        await db.Database.OpenConnectionAsync();
        var map = JsonSerializer.Serialize(new[] { new { branchId = branch.Id, productId = fee.Id,
            receitaCategoryId = (Guid?)null, entradasCategoryId = (Guid?)null } });
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT set_config('loto.m95_seed_map', {map}, false)");
        await db.Database.MigrateAsync();

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
        var open = await client.PostAuthAsync("/dailyclose", new RequestOpenDailyCloseJson { AccountId = account.Id, Date = date }, token);
        open.StatusCode.ShouldBe(HttpStatusCode.Created, await open.Content.ReadAsStringAsync());
        var close = await open.ReadContentAsync<ResponseDailyCloseJson>();
        var reviewResponse = await client.GetAuthAsync($"/dailyclose/{close.Id}/review", token);
        var review = await reviewResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        review.Items.ShouldNotContain(i => i.ProductId == fee.Id);
        review.Items.Where(i => !i.IsCashVarianceProduct).Sum(i => i.OpeningValue ?? 0m).ShouldBe(1_000m);
        var priorResponse = await client.GetAuthAsync($"/dailyclose/{prior.Id}/review", token);
        (await priorResponse.ReadContentAsync<ResponseDailyCloseReviewJson>()).Items
            .ShouldContain(i => i.ProductId == fee.Id && i.ClosingValue == 100m);
        var items = new[] { new RequestUpsertDailyCloseItemJson { ProductId = cash.Id, Value = 1_000m } };
        var preview = await client.PostAuthAsync($"/dailyclose/{close.Id}/variance-preview",
            new RequestDailyCloseVariancePreviewJson { Items = items }, token);
        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await preview.ReadContentAsync<ResponseDailyCloseVariancePreviewJson>()).CashVariance.ShouldBe(-100m);
        var save = await client.PutAuthAsync($"/dailyclose/{close.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson { Version = close.Version, Items = items,
                Notes = "Abertura inclui taxa histórica de R$100, visível no fechamento anterior." }, token);
        save.StatusCode.ShouldBe(HttpStatusCode.OK);
        var submit = await client.PostAuthAsync($"/dailyclose/{close.Id}/submit", token);
        submit.StatusCode.ShouldBe(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        (await submit.ReadContentAsync<ResponseDailyCloseJson>()).Items
            .Single(i => i.ProductId == variance.Id).Value.ShouldBe(-100m);
        var officialReview = await client.GetAuthAsync($"/dailyclose/{close.Id}/review", token);
        var official = await officialReview.ReadContentAsync<ResponseDailyCloseReviewJson>();
        official.Items.ShouldNotContain(i => i.ProductId == fee.Id);
        official.Items.Single(i => i.IsCashVarianceProduct).ClosingValue.ShouldBe(-100m);
        db.ChangeTracker.Clear();
        (await db.DailyCloses.SingleAsync(c => c.Id == prior.Id)).Version.ShouldBe(priorVersion);
        (await db.DailyCloseItems.SingleAsync(i => i.Id == priorFee.Id)).Value.ShouldBe(100m);
        (await db.Products.SingleAsync(p => p.Id == fee.Id)).Active.ShouldBeFalse();
    }
}
