using Microsoft.EntityFrameworkCore;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal sealed class FiadoProjectionRepository(ServerDbContext dbContext) : IFiadoProjectionRepository
{
    public async Task<decimal> GetHypotheticalSaleRemainderAsNoTracking(
        Guid branchId,
        Guid clientId,
        DateTime asOfDate,
        DateTime saleDueDate,
        DateTime saleDate,
        decimal saleValue,
        CancellationToken ct = default)
    {
        var rows = await FiadoNettingSql
            .Query<FiadoRemainderSqlRow>(
                dbContext,
                $$"""
                SELECT LEAST(CAST({5} AS numeric), GREATEST(0,
                           COALESCE((SELECT SUM(s."Value") FROM fiado s
                                     WHERE s."Direction" = {{(short)Direction.Out}}
                                       AND (s."DueDate" < CAST({3} AS date)
                                            OR (s."DueDate" = CAST({3} AS date) AND s."Date" <= CAST({4} AS date)))), 0)
                           + CAST({5} AS numeric)
                           - COALESCE((SELECT SUM(f."Value") FROM fiado f
                                       WHERE f."Direction" = {{(short)Direction.In}}), 0))) AS "Value"
                """,
                branchId,
                asOfDate,
                clientId,
                DateOnly.FromDateTime(saleDueDate),
                DateOnly.FromDateTime(saleDate),
                saleValue)
            .ToListAsync(ct);

        return rows.Single().Value;
    }
}

internal sealed class FiadoRemainderSqlRow
{
    public decimal Value { get; init; }
}
