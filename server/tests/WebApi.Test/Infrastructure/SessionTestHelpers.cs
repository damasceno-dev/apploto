using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using Shouldly;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// Drives Conferir abertura e iniciar through the API: reads the opening proposal and acknowledges it as proposed
/// (or as an explicit zero when nothing is proposed), optionally with differing values or another custodian.
/// </summary>
internal static class SessionTestHelpers
{
    extension(HttpClient client)
    {
        public async Task<HttpResponseMessage> PostStartSessionAsync(
            Guid accountId,
            string token,
            IReadOnlyDictionary<Guid, decimal>? acknowledged = null,
            Guid? custodianOperatorId = null,
            string? reason = null)
        {
            var opening = await client.BuildOpeningAcknowledgmentAsync(accountId, token, acknowledged, custodianOperatorId, reason);
            return await client.PostAuthAsync("/session/start", new RequestStartSessionJson { AccountId = accountId, Opening = opening }, token);
        }

        public async Task<ResponseStartSessionJson> StartSessionAsync(
            Guid accountId,
            string token,
            IReadOnlyDictionary<Guid, decimal>? acknowledged = null,
            Guid? custodianOperatorId = null,
            string? reason = null)
        {
            var response = await client.PostStartSessionAsync(accountId, token, acknowledged, custodianOperatorId, reason);
            response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponseStartSessionJson>();
        }

        public async Task<RequestSessionOpeningAcknowledgmentJson> BuildOpeningAcknowledgmentAsync(
            Guid accountId,
            string token,
            IReadOnlyDictionary<Guid, decimal>? acknowledged = null,
            Guid? custodianOperatorId = null,
            string? reason = null)
        {
            var proposalResponse = await client.GetAuthAsync($"/session/opening-proposal?accountId={accountId}", token);
            proposalResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await proposalResponse.Content.ReadAsStringAsync());
            var proposal = await proposalResponse.ReadContentAsync<ResponseSessionOpeningProposalJson>();

            var values = proposal.Items
                .Where(item => item.Value is not null || acknowledged?.ContainsKey(item.ProductId) == true)
                .ToDictionary(item => item.ProductId, item => item.Value ?? 0m);
            foreach (var (productId, value) in acknowledged ?? new Dictionary<Guid, decimal>())
                values[productId] = value;
            var items = proposal.IsMissing && acknowledged is null
                ? []
                : values.Select(pair => new RequestSessionOpeningItemJson { ProductId = pair.Key, Value = pair.Value }).ToList();

            return new RequestSessionOpeningAcknowledgmentJson
            {
                CustodianOperatorId = custodianOperatorId,
                SourceKind = proposal.SourceKind,
                SourceId = proposal.SourceId,
                SourceRevision = proposal.SourceRevision,
                ExplicitZero = items.All(item => item.Value == 0m),
                Items = items,
                Reason = reason
            };
        }
    }

    extension(ServerWebApplicationFactory factory)
    {
        /// <summary>Every branch has the system Diferença Caixa product; reads fail closed without it.</summary>
        public async Task EnsureCashVarianceProductAsync(Guid branchId)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<server.Infrastructure.ServerDbContext>();
            var exists = await dbContext.Products.AnyAsync(product =>
                product.BranchId == branchId &&
                product.Active &&
                product.Name == server.Application.Services.DailyCloses.CashVarianceProductResolver.CashVarianceProductName);
            if (exists is false)
                await factory.SeedProductAsync(branchId, server.Application.Services.DailyCloses.CashVarianceProductResolver.CashVarianceProductName, displayOrder: 999);
        }

        /// <summary>Runs raw SQL against the shared test database (fixture wiring and database-guard probes).</summary>
        public async Task ExecuteSqlAsync(string sql)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<server.Infrastructure.ServerDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync(sql);
        }

        /// <summary>
        /// Replaces the retired account/day open: seeds an uncounted Draft session of the cashbox for the date (the
        /// cashbox's latest session stays in custody) and returns it as the API reads it.
        /// </summary>
        public async Task<ResponseDailyCloseJson> OpenSeededSessionAsync(
            HttpClient client,
            Guid accountId,
            DateTime date,
            string token,
            Guid? custodianOperatorId = null,
            Guid? openedByUserId = null)
        {
            var account = await factory.ReloadAsync<server.Domain.Entities.Account>(accountId);
            account.ShouldNotBeNull();
            await factory.EnsureCashVarianceProductAsync(account.BranchId);
            var session = await factory.SeedDailyCloseAsync(
                account.BranchId,
                accountId,
                date,
                itemsRecorded: false,
                custodianOperatorId: custodianOperatorId,
                openedByUserId: openedByUserId);
            var response = await client.GetAuthAsync($"/session/{session.Id}", token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponseDailyCloseJson>();
        }
    }
}
