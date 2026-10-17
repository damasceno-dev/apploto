using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using server.Domain.Entities.Enums;

namespace server.Infrastructure.Repositories;

/// <summary>
/// The one authoritative fiado read projection (§6.4, M7.7 Phase 5.1). Every fiado balance, aging and statement
/// read composes its final SELECT over these CTEs, so they cannot drift apart:
/// <list type="bullet">
/// <item><c>fiado</c> — active (<c>Active</c> and <c>Status = Active</c>) Tab rows with a client, dated on or before
/// the as-of date, in the branch (optionally one client). <c>Signed</c> is <c>+Value</c> for Out, <c>−Value</c> for
/// In. Every Tab participates: netting is branch/client-wide, never per account.</item>
/// <item><c>client_in</c> — each client's cumulative In.</item>
/// <item><c>open_sales</c> — FIFO: the client's Out rows ordered <c>DueDate, Date, Id</c> with a running Out total;
/// cumulative In is subtracted oldest-first and only rows with a positive remainder survive, carrying
/// <c>Remaining = LEAST(Value, CumulativeOut − TotalIn)</c>. Repayment In rows never appear and a client in
/// credit has no open sale. <c>PaidAt</c> plays no part.</item>
/// </list>
/// Placeholders <c>{0}</c> branch id, <c>{1}</c> as-of date and <c>{2}</c> optional client id are reserved by the
/// CTEs; the caller's SELECT may use <c>{3}</c> onward. Nothing is persisted or materialized.
/// </summary>
internal static class FiadoNettingSql
{
    private static readonly string Ctes = $$"""
        WITH fiado AS (
            SELECT t."Id", t."ClientId", t."AccountId", t."TransactionTypeId", t."Date", t."DueDate", t."Direction",
                   t."Value", t."Description", t."CreatedAt",
                   CASE WHEN t."Direction" = {{(short)Direction.Out}} THEN t."Value" ELSE -t."Value" END AS "Signed"
            FROM "Transactions" t
            INNER JOIN "Accounts" a ON a."Id" = t."AccountId"
            WHERE t."BranchId" = {0}
              AND t."Active"
              AND t."Status" = {{(short)TransactionStatus.Active}}
              AND a."Type" = {{(int)AccountType.Tab}}
              AND t."ClientId" IS NOT NULL
              AND t."Date" <= CAST({1} AS date)
              AND (CAST({2} AS uuid) IS NULL OR t."ClientId" = CAST({2} AS uuid))
        ),
        client_in AS (
            SELECT f."ClientId", COALESCE(SUM(f."Value") FILTER (WHERE f."Direction" = {{(short)Direction.In}}), 0) AS "TotalIn"
            FROM fiado f
            GROUP BY f."ClientId"
        ),
        sales AS (
            SELECT f.*,
                   SUM(f."Value") OVER (
                       PARTITION BY f."ClientId"
                       ORDER BY f."DueDate", f."Date", f."Id"
                       ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS "CumulativeOut"
            FROM fiado f
            WHERE f."Direction" = {{(short)Direction.Out}}
        ),
        open_sales AS (
            SELECT s.*, LEAST(s."Value", s."CumulativeOut" - ci."TotalIn") AS "Remaining"
            FROM sales s
            INNER JOIN client_in ci ON ci."ClientId" = s."ClientId"
            WHERE s."CumulativeOut" - ci."TotalIn" > 0
        )

        """;

    public static IQueryable<T> Query<T>(
        ServerDbContext dbContext,
        string select,
        Guid branchId,
        DateTime asOfDate,
        Guid? clientId,
        params object?[] extra)
    {
        object?[] arguments = [branchId, DateOnly.FromDateTime(asOfDate), clientId, .. extra];
        return dbContext.Database.SqlQuery<T>(FormattableStringFactory.Create(Ctes + select, arguments));
    }
}

internal sealed class FiadoBalanceSqlRow
{
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public decimal OutstandingTotal { get; init; }
}

internal sealed class FiadoOpenSaleSqlRow
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Value { get; init; }
    public Guid ClientId { get; init; }
    public string ClientName { get; init; } = string.Empty;
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string? Description { get; init; }
}

internal sealed class FiadoStatementSqlRow
{
    public Guid Id { get; init; }
    public DateTime Date { get; init; }
    public short Direction { get; init; }
    public decimal Value { get; init; }
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public string TransactionTypeName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public decimal RunningBalance { get; init; }
}

internal sealed class FiadoStatementWindowSqlRow
{
    public decimal OpeningBalance { get; init; }
    public decimal ClosingBalance { get; init; }
    public int TotalCount { get; init; }
}

internal sealed class FiadoCountSqlRow
{
    public int Count { get; init; }
}

internal sealed class FiadoPositionSqlRow
{
    public decimal OutstandingTotal { get; init; }
    public DateTime? OldestUnpaidDate { get; init; }
}
