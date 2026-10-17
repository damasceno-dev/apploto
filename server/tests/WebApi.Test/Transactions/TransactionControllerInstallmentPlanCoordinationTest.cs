using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Infrastructure;
using server.Infrastructure.Services;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Transactions;

/// <summary>
/// M7.7 Phase 7.6.2: the plan finalize serializes with every other ledger write on the plan account's
/// coordination key and commits all installments or none. Requests are queued behind a held advisory
/// key and released in a known order.
/// </summary>
[Collection(ServerApiCollection.Name)]
public sealed class TransactionControllerInstallmentPlanCoordinationTest(ServerWebApplicationFactory factory)
{
    private static readonly TimeSpan GrantOrderTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task TwoConcurrentFinalizes_ShouldActivateThePlanOnce()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanRaceTwoFinalizes", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var firstClient = grantOrderFactory.CreateClient();
        using var secondClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldAccountKeyAsync(ctx.BranchId, ctx.BankAccountId);

        var firstTask = Finalize(firstClient, originId, plan.Installments, ctx.Token);
        await heldLock.WaitForWaitersAsync(1);
        var secondTask = Finalize(secondClient, originId, plan.Installments, ctx.Token);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(10));
        var first = await firstTask;
        var second = await secondTask;

        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await second.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_NOT_DRAFT);
        var after = await GetPlan(originId, ctx.Token);
        after.ActiveCount.ShouldBe(3);
        after.TotalValue.ShouldBe(300m);
    }

    [Fact]
    public async Task CancelFirst_ShouldMakeTheQueuedFinalizeRefuseTheChangedPlan()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanRaceCancelFirst", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);
        var cancelled = plan.Installments[0];
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var cancelClient = grantOrderFactory.CreateClient();
        using var finalizeClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldAccountKeyAsync(ctx.BranchId, ctx.BankAccountId);

        var cancelTask = Cancel(cancelClient, cancelled, ctx.Token);
        await heldLock.WaitForWaitersAsync(1);
        var finalizeTask = Finalize(finalizeClient, originId, plan.Installments, ctx.Token);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(cancelTask, finalizeTask).WaitAsync(TimeSpan.FromSeconds(10));
        var cancel = await cancelTask;
        var finalize = await finalizeTask;

        cancel.StatusCode.ShouldBe(HttpStatusCode.OK, await cancel.Content.ReadAsStringAsync());
        finalize.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await finalize.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_MIXED_CONFIRMATION_REQUIRED);
        var after = await GetPlan(originId, ctx.Token);
        after.Status.ShouldBe(InstallmentPlanStatus.Mixed);
        after.ActiveCount.ShouldBe(0);
    }

    [Fact]
    public async Task FinalizeFirst_ShouldMakeTheQueuedCancelOfAnOldVersionStale()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanRaceFinalizeFirst", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);
        await using var grantOrderFactory = CreateGrantOrderFactory();
        using var finalizeClient = grantOrderFactory.CreateClient();
        using var cancelClient = grantOrderFactory.CreateClient();
        await using var heldLock = await HoldAccountKeyAsync(ctx.BranchId, ctx.BankAccountId);

        var finalizeTask = Finalize(finalizeClient, originId, plan.Installments, ctx.Token);
        await heldLock.WaitForWaitersAsync(1);
        var cancelTask = Cancel(cancelClient, plan.Installments[1], ctx.Token);
        await heldLock.WaitForWaitersAsync(2);

        await heldLock.ReleaseAsync();
        await Task.WhenAll(finalizeTask, cancelTask).WaitAsync(TimeSpan.FromSeconds(10));
        var finalize = await finalizeTask;
        var cancel = await cancelTask;

        finalize.StatusCode.ShouldBe(HttpStatusCode.OK, await finalize.Content.ReadAsStringAsync());
        cancel.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await cancel.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_STALE_WRITE);
        (await GetPlan(originId, ctx.Token)).ActiveCount.ShouldBe(2);
    }

    [Fact]
    public async Task Finalize_ShouldLeaveEveryInstallmentDraft_WhenOneInstallmentCannotBeStored()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanRollback", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);
        var lastId = plan.Installments[^1].Id;
        var suffix = lastId.ToString("N");
        using var client = factory.CreateClient();

        await ExecuteSql(
            $$"""
            CREATE FUNCTION reject_plan_row_{{suffix}}() RETURNS trigger AS $$
            BEGIN
                IF NEW."Id" = '{{lastId}}' AND NEW."Status" <> OLD."Status" THEN
                    RAISE EXCEPTION 'installment store unavailable (test)';
                END IF;
                RETURN NEW;
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER reject_plan_row_{{suffix}}
            BEFORE UPDATE ON "Transactions"
            FOR EACH ROW EXECUTE FUNCTION reject_plan_row_{{suffix}}();
            """);
        try
        {
            var response = await Finalize(client, originId, plan.Installments, ctx.Token);

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        finally
        {
            await ExecuteSql(
                $"""
                DROP TRIGGER IF EXISTS reject_plan_row_{suffix} ON "Transactions";
                DROP FUNCTION IF EXISTS reject_plan_row_{suffix}();
                """);
        }

        foreach (var installment in plan.Installments)
            (await factory.ReloadAsync<Transaction>(installment.Id))!.Status.ShouldBe(TransactionStatus.Draft);

        var retry = await Finalize(client, originId, plan.Installments, ctx.Token);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        (await GetPlan(originId, ctx.Token)).ActiveCount.ShouldBe(3);
    }

    [Fact]
    public async Task Finalize_ShouldReturnThePlanStaleError_WhenAPerRowEditLandsJustBeforeTheSave()
    {
        var ctx = await InstallmentPlanTestContext.SeedAsync(factory, "PlanSaveTimeConflict", Role.Manager);
        var originId = await ctx.SeedPlanAsync(TransactionStatus.Draft, TransactionStatus.Draft, TransactionStatus.Draft);
        var plan = await GetPlan(originId, ctx.Token);
        var editedId = plan.Installments[1].Id;
        var connectionString = await GetConnectionString();
        // The test host's unit of work commits a concurrent per-row edit (outside the request's transaction,
        // as a PUT /transaction/{id} would) after the command's checks and right before its save.
        await using var racingFactory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            var original = services.Single(descriptor => descriptor.ServiceType == typeof(IUnitOfWork));
            services.Remove(original);
            services.AddScoped<IUnitOfWork>(provider => new EditBeforeSaveUnitOfWork(
                (IUnitOfWork)ActivatorUtilities.CreateInstance(provider, original.ImplementationType!),
                connectionString,
                editedId));
        }));
        using var client = racingFactory.CreateClient();

        var response = await Finalize(client, originId, plan.Installments, ctx.Token);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
            .ShouldContain(ResourcesErrorMessages.TRANSACTION_PLAN_STALE_WRITE);
        foreach (var installment in plan.Installments)
            (await factory.ReloadAsync<Transaction>(installment.Id))!.Status.ShouldBe(TransactionStatus.Draft);
        (await factory.ReloadAsync<Transaction>(editedId))!.Description.ShouldBe("Editado durante a finalização");
    }

    private async Task<string> GetConnectionString()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<ServerDbContext>().Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        return connectionString;
    }

    private sealed class EditBeforeSaveUnitOfWork(IUnitOfWork inner, string connectionString, Guid editedId) : IUnitOfWork
    {
        public Task Commit(CancellationToken ct = default) => inner.Commit(ct);

        public async Task<bool> TryCommitOptimistic(CancellationToken ct = default)
        {
            await using (var connection = new NpgsqlConnection(connectionString))
            {
                await connection.OpenAsync(ct);
                await using var command = new NpgsqlCommand(
                    "UPDATE \"Transactions\" SET \"Description\" = 'Editado durante a finalização' WHERE \"Id\" = @id",
                    connection);
                command.Parameters.AddWithValue("id", editedId);
                await command.ExecuteNonQueryAsync(ct);
            }

            return await inner.TryCommitOptimistic(ct);
        }
    }

    private WebApplicationFactory<Program> CreateGrantOrderFactory()
    {
        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DailyCloseAccountCoordinationOptions>();
            services.AddSingleton(new DailyCloseAccountCoordinationOptions(GrantOrderTimeout));
        }));
    }

    private async Task<HeldAdvisoryLock> HoldAccountKeyAsync(Guid branchId, Guid accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var connectionString = dbContext.Database.GetConnectionString();
        connectionString.ShouldNotBeNullOrWhiteSpace();
        return await HeldAdvisoryLock.AcquireAsync(connectionString, DailyCloseAccountCoordinationKey.Compute(branchId, accountId));
    }

    private async Task ExecuteSql(string sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await dbContext.Database.ExecuteSqlRawAsync(sql);
    }

    private async Task<ResponseInstallmentPlanJson> GetPlan(Guid originId, string token)
    {
        using var client = factory.CreateClient();
        var response = await client.GetAuthAsync($"/transaction/installment/{originId}", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseInstallmentPlanJson>();
    }

    private static Task<HttpResponseMessage> Finalize(
        HttpClient client,
        Guid originId,
        IEnumerable<ResponseTransactionJson> installments,
        string token)
    {
        return client.PostAuthAsync(
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

    private static Task<HttpResponseMessage> Cancel(HttpClient client, ResponseTransactionJson installment, string token)
    {
        return client.PostAuthAsync(
            $"/transaction/{installment.Id}/cancel",
            new RequestCancelTransactionJson { CancellationReason = "Cheque devolvido" },
            token,
            expectedVersion: installment.Version);
    }
}
