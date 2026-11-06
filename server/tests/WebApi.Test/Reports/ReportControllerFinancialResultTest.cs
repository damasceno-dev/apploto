using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Reports;

[Collection(ServerApiCollection.Name)]
public class ReportControllerFinancialResultTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task FrozenClasses_OfficialVariance_UnknownAndPagination_HaveIndependentExactTotals()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026, 11, 5, 10, 0, 0));
        var (user, branch, _, token) = await factory.SeedFullBranchContextAsync("FinancialClassResult", Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.BankAccount);
        var op = await factory.SeedOperatorAsync(branch.Id);
        var variance = await factory.SeedProductAsync(branch.Id, "Diferença Caixa");
        var rows = new[]
        {
            (FinancialClass.CaixaAndClientMoney, Direction.Out, 100m),
            (FinancialClass.ShopFeesAndIncome, Direction.In, 40m),
            (FinancialClass.OperatingExpenseAndLoss, Direction.Out, 7m),
            (FinancialClass.InternalTransfer, Direction.Out, 300m),
            (FinancialClass.Unclassified, Direction.In, 11m)
        };
        var types = new List<Guid>();
        foreach (var (classification, direction, value) in rows)
        {
            var category = await factory.SeedCategoryAsync(branch.Id, defaultDirection: direction);
            var type = await factory.SeedTransactionTypeAsync(category.Id, financialClass: classification);
            types.Add(type.Id);
            await factory.SeedTransactionAsync(branch.Id, account.Id, type.Id, category.Id, direction, op.Id, user.Id,
                date: new DateTime(2026, 10, 10), value: value);
        }
        var cashbox = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var official = await factory.SeedDailyCloseAsync(branch.Id, cashbox.Id, new DateTime(2026, 10, 10), DailyCloseStatus.Approved);
        await factory.SeedDailyCloseItemAsync(official.Id, variance.Id, -2m);
        var draft = await factory.SeedDailyCloseAsync(branch.Id, cashbox.Id, new DateTime(2026, 10, 11), DailyCloseStatus.Draft);
        await factory.SeedDailyCloseItemAsync(draft.Id, variance.Id, 999m);
        async Task<ResponseFinancialResultJson> Read(string query = "")
        {
            var response = await client.GetAuthAsync("/report/financial-result?year=2026&month=10&pageSize=" + (query.Contains("pageSize=50") ? "50" : "2") + query.Replace("&pageSize=50", ""), token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponseFinancialResultJson>();
        }
        var first = await Read();
        first.TotalCount.ShouldBe(6); first.Entries.Count.ShouldBe(2);
        first.KnownOwnResult.ShouldBe(33m);
        first.ClassificationComplete.ShouldBeFalse(); first.FeeAmountsComplete.ShouldBeTrue(); first.BankPositionComplete.ShouldBeFalse();
        first.Classes.Single(c => c.FinancialClass == FinancialClass.CashDifference).KnownSignedTotal.ShouldBe(-2m);
        first.Classes.Single(c => c.FinancialClass == FinancialClass.InternalTransfer).KnownVolume.ShouldBe(300m);
        var filtered = await Read("&financialClass=ShopFeesAndIncome");
        filtered.Classes.ShouldHaveSingleItem().FinancialClass.ShouldBe(FinancialClass.ShopFeesAndIncome);
        filtered.KnownOwnResult.ShouldBe(40m);
        filtered.ClassificationComplete.ShouldBeFalse(); // Unknown rows outside this filter still qualify the month.
        var second = await Read("&page=2");
        second.KnownOwnResult.ShouldBe(33m);
        second.Entries.Select(e => e.SourceId).Intersect(first.Entries.Select(e => e.SourceId)).ShouldBeEmpty();
        var tab = await factory.SeedAccountAsync(branch.Id, AccountType.Tab);
        var debtor = await factory.SeedClientAsync(branch.Id);
        var collectionCategory = await factory.SeedCategoryAsync(branch.Id, defaultDirection: Direction.In);
        var misconfiguredCollection = await factory.SeedTransactionTypeAsync(collectionCategory.Id,
            requiresTabAccountAndClient: true, fiadoPaymentKind: FiadoPaymentKind.NonCash,
            financialClass: FinancialClass.ShopFeesAndIncome);
        var principal = await factory.SeedTransactionAsync(branch.Id, tab.Id, misconfiguredCollection.Id, collectionCategory.Id,
            Direction.In, op.Id, user.Id, date: new DateTime(2026, 10, 10), value: 200m, clientId: debtor.Id);
        var principalResult = await Read("&pageSize=50");
        principalResult.KnownOwnResult.ShouldBe(33m);
        // Receipt of principal is never revenue, even with a contradictory frozen configuration.
        var conflict = principalResult.Entries.Single(e => e.SourceId == principal.Id);
        conflict.ClassificationConflict.ShouldBeTrue();
        conflict.SnapshotFinancialClass.ShouldBe(FinancialClass.ShopFeesAndIncome);
        conflict.FinancialClass.ShouldBe(FinancialClass.Unclassified);
        await factory.SeedSettingAsync(branch.Id, lockDate: new DateTime(2026, 10, 31));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            await db.TransactionTypes.Where(t => types.Contains(t.Id)).ExecuteUpdateAsync(s => s
                .SetProperty(t => t.FinancialClass, FinancialClass.ShopFeesAndIncome).SetProperty(t => t.Name, "Renamed"));
        }
        (await Read()).KnownOwnResult.ShouldBe(33m);
        (await client.GetAuthAsync("/report/financial-result?year=2026&month=10&pageSize=201", token)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await client.GetAuthAsync("/report/financial-result?year=2026&month=12", token)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var outsider = await factory.SeedFullBranchContextAsync("FinancialClassOther", Role.Manager);
        await factory.SeedProductAsync(outsider.Branch.Id, "Diferença Caixa");
        var empty = await client.GetAuthAsync("/report/financial-result?year=2026&month=10", outsider.Token);
        (await empty.ReadContentAsync<ResponseFinancialResultJson>()).TotalCount.ShouldBe(0);
    }
}
