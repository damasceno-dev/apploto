using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Branches.Create;

[Collection(ServerApiCollection.Name)]
public class CreateBranchSeedPersistenceTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_ShouldPersistTransactionTypeSettlementMetadata()
    {
        var user = await factory.SeedUserAsync();
        var token = factory.IssueGlobalToken(user);
        var request = new RequestCreateBranchJsonBuilder()
            .WithName($"Seed Metadata {Guid.NewGuid():N}")
            .Build();

        var httpResponse = await _client.PostAuthAsync("/branch/create", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateBranchJson>();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var persistedRows = await dbContext.TransactionTypes
            .AsNoTracking()
            .Include(transactionType => transactionType.Category)
            .Where(transactionType => transactionType.Category.BranchId == payload.Id)
            .Select(transactionType => new
            {
                transactionType.Name,
                CategoryName = transactionType.Category.Name,
                transactionType.SettlementRule,
                transactionType.RequiresTabAccountAndClient
            })
            .ToListAsync();

        persistedRows.Count.ShouldBe(19);

        foreach (var (seed, expected) in ExpectedTransactionTypeMetadata())
        {
            persistedRows.ShouldContain(row =>
                row.Name == seed.Name &&
                row.CategoryName == seed.CategoryName &&
                row.SettlementRule == expected.SettlementRule &&
                row.RequiresTabAccountAndClient == expected.RequiresTabAccountAndClient);
        }
    }

    private static IReadOnlyDictionary<(string Name, string CategoryName), (SettlementRule SettlementRule, bool RequiresTabAccountAndClient)>
        ExpectedTransactionTypeMetadata()
    {
        return new Dictionary<(string Name, string CategoryName), (SettlementRule SettlementRule, bool RequiresTabAccountAndClient)>
        {
            [("Cliente", "Saídas")] = (SettlementRule.SameDay, true),
            [("Depósito Dinheiro", "Saídas")] = (SettlementRule.NextCalendarDay, false),
            [("Cartão de Crédito", "Saídas")] = (SettlementRule.TwoBusinessDays, false),
            [("MarketPlace", "Saídas")] = (SettlementRule.SameDay, false),
            [("Sobra de Bolão", "Despesas Comerciais")] = (SettlementRule.SameDay, false),
            [("Sobra de Federal", "Despesas Comerciais")] = (SettlementRule.SameDay, false),
            [("Depósito Cheque", "Saídas")] = (SettlementRule.OperatorEnteredCheque, false),
            [("PIX", "Saídas")] = (SettlementRule.SameDay, false),
            [("Cartão de Débito", "Saídas")] = (SettlementRule.NextBusinessDay, false),
            [("Telesena", "Saídas")] = (SettlementRule.SameDay, false),
            [("Troca de Telesena", "Saídas")] = (SettlementRule.SameDay, false),
            [("Raspadinha", "Saídas")] = (SettlementRule.SameDay, false),
            [("Encalhe Federal", "Saídas")] = (SettlementRule.SameDay, false),
            [("Cliente", "Entradas")] = (SettlementRule.SameDay, true),
            [("Pgto Prêmio", "Saídas")] = (SettlementRule.SameDay, false),
            [("Desconto", "Despesas Comerciais")] = (SettlementRule.SameDay, false),
            [("Volante rejeitado", "Despesas Comerciais")] = (SettlementRule.SameDay, false),
            [("Tarifa cartão", "Despesas Comerciais")] = (SettlementRule.SameDay, false),
            [("Outras Despesas", "Despesas Comerciais")] = (SettlementRule.SameDay, false)
        };
    }
}
