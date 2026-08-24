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

    [Fact]
    public async Task Create_ShouldPersistSettingAndInitialTimeEntryPolicy()
    {
        var user = await factory.SeedUserAsync();
        var token = factory.IssueGlobalToken(user);
        var request = new RequestCreateBranchJsonBuilder()
            .WithName($"Seed TimeEntryPolicy {Guid.NewGuid():N}")
            .Build();

        var httpResponse = await _client.PostAuthAsync("/branch/create", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateBranchJson>();

        // Real database reload — not a mocked Received(Add) assertion — so a later
        // EF/repository/transaction regression that drops either row before commit
        // surfaces here instead of only in a UseCases.Test mock expectation.
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var persistedSetting = await dbContext.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(setting => setting.BranchId == payload.Id);
        persistedSetting.ShouldNotBeNull();
        persistedSetting.DailyTargetHours.ShouldBe(7.33m);
        persistedSetting.LunchDeductionOver6H.ShouldBe(1.00m);
        persistedSetting.LunchDeductionOver4H.ShouldBe(0.25m);

        var persistedPolicy = await dbContext.TimeEntryPolicies
            .AsNoTracking()
            .SingleOrDefaultAsync(policy => policy.BranchId == payload.Id);
        persistedPolicy.ShouldNotBeNull();
        persistedPolicy.EffectiveFrom.ShouldBe(DateTime.MinValue);
        persistedPolicy.DailyTargetHours.ShouldBe(persistedSetting.DailyTargetHours);
        persistedPolicy.LunchDeductionOver6H.ShouldBe(persistedSetting.LunchDeductionOver6H);
        persistedPolicy.LunchDeductionOver4H.ShouldBe(persistedSetting.LunchDeductionOver4H);
        persistedPolicy.Active.ShouldBeTrue();
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
