using server.Domain.Interfaces;

namespace server.Application.Services.DailyCloses;

public class CashVarianceProductResolver(IProductsRepository productsRepository) : ICashVarianceProductResolver
{
    internal const string CashVarianceProductName = "Diferença Caixa";

    public async Task<Guid> GetIdAsync(Guid branchId, CancellationToken ct = default)
    {
        var product = await productsRepository.GetActiveByBranchIdAndNameAsNoTracking(
            branchId,
            CashVarianceProductName,
            ct);

        if (product is null)
        {
            throw new InvalidOperationException(
                $"Bootstrap defect: the seeded product \"{CashVarianceProductName}\" was not found for branch {branchId}. " +
                "Ensure CreateBranchSeedFactory ran correctly for this branch.");
        }

        return product.Id;
    }
}
