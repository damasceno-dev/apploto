using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.DailyCloses;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.DailyCloses;

/// <summary>
/// M7.7 Phase 7.6.1: a review event commits with the command that caused it or not at all. A
/// test-scoped PostgreSQL trigger rejects event inserts for one branch only, so the command fails
/// after its business changes were staged; nothing from that command may survive.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class DailyCloseControllerHistoryAtomicityTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Correction_ShouldRollBackItemsDemotionAndEvent_WhenTheEventCannotBeStored()
    {
        var ctx = await SeedContext("HistoryAtomicCorrection");
        var source = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Today.AddDays(-5), DailyCloseStatus.Draft);
        await factory.SeedDailyCloseItemAsync(source.Id, ctx.ProductId, 100m);
        var successor = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Today.AddDays(-4), DailyCloseStatus.Submitted);
        var version = await GetVersion(source.Id, ctx.Token);

        await using (await RejectEventInsertsFor(ctx.BranchId))
        {
            var response = await _client.PutAuthAsync(
                $"/dailyclose/{source.Id}/items",
                new VersionedRequestPutDailyCloseItemsJson
                {
                    Version = version,
                    Items = [new RequestUpsertDailyCloseItemJson { ProductId = ctx.ProductId, Value = 150m }]
                },
                ctx.Token);

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }

        var reloadedSource = await LoadWithItems(source.Id);
        reloadedSource.Items.Where(item => item.Active).ShouldHaveSingleItem().Value.ShouldBe(100m);
        var reloadedSuccessor = await factory.ReloadAsync<DailyClose>(successor.Id);
        reloadedSuccessor!.Status.ShouldBe(DailyCloseStatus.Submitted);
        reloadedSuccessor.OpeningRecheckRequiredAt.ShouldBeNull();
        (await CountEvents(ctx.BranchId)).ShouldBe(0);

        // Once events can be stored again the same correction succeeds and records exactly one event.
        var retry = await _client.PutAuthAsync(
            $"/dailyclose/{source.Id}/items",
            new VersionedRequestPutDailyCloseItemsJson
            {
                Version = version,
                Items = [new RequestUpsertDailyCloseItemJson { ProductId = ctx.ProductId, Value = 150m }]
            },
            ctx.Token);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        (await CountEvents(ctx.BranchId)).ShouldBe(1);
    }

    [Fact]
    public async Task Reject_ShouldRollBackTheRejection_WhenTheEventCannotBeStored()
    {
        var ctx = await SeedContext("HistoryAtomicReject");
        var close = await factory.SeedDailyCloseAsync(ctx.BranchId, ctx.AccountId, ctx.Today.AddDays(-2), DailyCloseStatus.Submitted);

        await using (await RejectEventInsertsFor(ctx.BranchId))
        {
            var response = await _client.PostAuthAsync(
                $"/dailyclose/{close.Id}/reject",
                new RequestRejectDailyCloseJson { RejectionReason = "Recontar" },
                ctx.Token);

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }

        var reloaded = await factory.ReloadAsync<DailyClose>(close.Id);
        reloaded!.Status.ShouldBe(DailyCloseStatus.Submitted);
        reloaded.RejectionReason.ShouldBeNull();
        (await CountEvents(ctx.BranchId)).ShouldBe(0);
    }

    private async Task<IAsyncDisposable> RejectEventInsertsFor(Guid branchId)
    {
        var suffix = branchId.ToString("N");
        await ExecuteSql(
            $$"""
            CREATE FUNCTION reject_review_event_{{suffix}}() RETURNS trigger AS $$
            BEGIN
                IF NEW."BranchId" = '{{branchId}}' THEN
                    RAISE EXCEPTION 'review event store unavailable (test)';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER reject_review_event_{{suffix}}
            BEFORE INSERT ON "DailyCloseReviewEvents"
            FOR EACH ROW EXECUTE FUNCTION reject_review_event_{{suffix}}();
            """);
        return new TriggerCleanup(this, suffix);
    }

    private async Task ExecuteSql(string sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<int> CountEvents(Guid branchId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.DailyCloseReviewEvents.CountAsync(reviewEvent => reviewEvent.BranchId == branchId);
    }

    private async Task<DailyClose> LoadWithItems(Guid dailyCloseId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        return await dbContext.DailyCloses
            .AsNoTracking()
            .Include(dailyClose => dailyClose.Items)
            .SingleAsync(dailyClose => dailyClose.Id == dailyCloseId);
    }

    private async Task<uint> GetVersion(Guid dailyCloseId, string token)
    {
        var response = await _client.GetAuthAsync($"/dailyclose/{dailyCloseId}", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadContentAsync<ResponseDailyCloseJson>()).Version;
    }

    private async Task<AtomicityContext> SeedContext(string label)
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync(label, Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id, displayOrder: 1);
        await factory.SeedProductAsync(branch.Id, CashVarianceProductResolver.CashVarianceProductName, displayOrder: 99);
        return new AtomicityContext(branch.Id, account.Id, product.Id, token, new BranchClock().LocalBusinessDate(DateTime.UtcNow));
    }

    private sealed record AtomicityContext(Guid BranchId, Guid AccountId, Guid ProductId, string Token, DateTime Today);

    private sealed class TriggerCleanup(DailyCloseControllerHistoryAtomicityTest test, string suffix) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await test.ExecuteSql(
                $"""
                DROP TRIGGER IF EXISTS reject_review_event_{suffix} ON "DailyCloseReviewEvents";
                DROP FUNCTION IF EXISTS reject_review_event_{suffix}();
                """);
        }
    }
}
