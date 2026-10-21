using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Domain.Models.Projections;

namespace server.Infrastructure.Repositories;

internal class TransactionsRepository(ServerDbContext dbContext) : ITransactionsRepository
{
    public async Task Add(Transaction transaction, CancellationToken ct = default)
    {
        await dbContext.Transactions.AddAsync(transaction, ct);
    }

    public async Task AddRange(IEnumerable<Transaction> transactions, CancellationToken ct = default)
    {
        await dbContext.Transactions.AddRangeAsync(transactions, ct);
    }

    public async Task<Transaction?> GetByIdAndBranchId(Guid id, Guid branchId, CancellationToken ct = default)
    {
        return await dbContext.Transactions
            .Include(transaction => transaction.TransactionType)
            .ThenInclude(transactionType => transactionType.Category)
            .FirstOrDefaultAsync(transaction =>
                transaction.Id == id &&
                transaction.BranchId == branchId,
                ct);
    }

    public async Task<Transaction?> GetGeneratedBySourceIdAndBranchId(Guid sourceId, Guid branchId, CancellationToken ct = default)
    {
        return await dbContext.Transactions
            .FirstOrDefaultAsync(t => t.GeneratedFromTransactionId == sourceId && t.BranchId == branchId, ct);
    }

    public async Task<Transaction?> GetGeneratedBySourceIdAndBranchIdAsNoTracking(
        Guid sourceId,
        Guid branchId,
        CancellationToken ct = default)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.GeneratedFromTransactionId == sourceId && t.BranchId == branchId, ct);
    }

    public async Task<Transaction?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(transaction =>
                transaction.Id == id &&
                transaction.BranchId == branchId,
                ct);
    }

    public async Task<Transaction?> GetByIdAndBranchIdAsNoTrackingWithTransactionType(Guid id, Guid branchId)
    {
        // No-tracking twin of GetByIdAndBranchId for the edit-impact preview. Mirrors the
        // tracked load's TransactionType -> Category include (the use case reads
        // RequiresTabAccountAndClient) and additionally includes Account so the hypothetical
        // overlay can read Account.Type without a second round-trip.
        return await dbContext.Transactions
            .AsNoTracking()
            .Include(transaction => transaction.TransactionType)
            .ThenInclude(transactionType => transactionType.Category)
            .Include(transaction => transaction.Account)
            .FirstOrDefaultAsync(transaction =>
                transaction.Id == id &&
                transaction.BranchId == branchId);
    }

    public async Task<IReadOnlyList<Transaction>> ListByBranchIdAsNoTracking(
        Guid branchId,
        TransactionListFilter filter)
    {
        // Single branch-scoped projection: include the joined names so the response
        // can carry AccountName / ClientName / TransactionTypeName without forcing
        // frontend N+1 lookups. EF translates this into one SQL query with LEFT JOINs.
        return await ApplyFilter(dbContext.Transactions, branchId, filter)
            .AsNoTracking()
            .Include(transaction => transaction.Account)
            .Include(transaction => transaction.Client)
            .Include(transaction => transaction.TransactionType)
            .OrderByDescending(transaction => transaction.Date)
            .ThenByDescending(transaction => transaction.TransactionTime.HasValue)
            .ThenByDescending(transaction => transaction.TransactionTime)
            .ThenByDescending(transaction => transaction.CreatedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
    }

    public async Task<int> CountByBranchIdAsNoTracking(Guid branchId, TransactionListFilter filter)
    {
        return await ApplyFilter(dbContext.Transactions, branchId, filter)
            .AsNoTracking()
            .CountAsync();
    }

    public async Task<IReadOnlyList<Transaction>> ListByOriginTransactionIdAndBranchIdAsNoTracking(
        Guid originId,
        Guid branchId)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.OriginTransactionId == originId &&
                transaction.BranchId == branchId)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Transaction>> ListPlanInstallmentsByOriginTransactionIdAndBranchId(
        Guid originTransactionId,
        Guid branchId,
        CancellationToken ct = default)
    {
        return await BuildPlanInstallmentsQuery(dbContext.Transactions, originTransactionId, branchId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Transaction>> ListPlanInstallmentsByOriginTransactionIdAndBranchIdAsNoTracking(
        Guid originTransactionId,
        Guid branchId,
        CancellationToken ct = default)
    {
        return await BuildPlanInstallmentsQuery(dbContext.Transactions.AsNoTracking(), originTransactionId, branchId)
            .ToListAsync(ct);
    }

    private static IQueryable<Transaction> BuildPlanInstallmentsQuery(
        IQueryable<Transaction> source,
        Guid originTransactionId,
        Guid branchId)
    {
        return source
            .Where(transaction =>
                transaction.OriginTransactionId == originTransactionId &&
                transaction.BranchId == branchId &&
                transaction.Active)
            .OrderBy(transaction => transaction.DueDate)
            .ThenBy(transaction => transaction.Id);
    }

    public async Task<decimal> SumActiveValueByAccountAndDateAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime date,
        Direction? direction = null,
        CancellationToken ct = default)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.BranchId == branchId &&
                transaction.AccountId == accountId &&
                transaction.Date == date &&
                transaction.Active &&
                transaction.Status == TransactionStatus.Active);

        if (direction is { } selectedDirection)
        {
            query = query.Where(transaction => transaction.Direction == selectedDirection);
        }

        return await query.Select(transaction => (decimal?)transaction.Value).SumAsync(ct) ?? 0m;
    }

    public async Task<DateTime?> GetEarliestUncountedActivityDateByAccountAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime afterDateExclusive,
        DateTime beforeDateExclusive,
        CancellationToken ct = default)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.BranchId == branchId &&
                transaction.AccountId == accountId &&
                transaction.Active &&
                transaction.Status == TransactionStatus.Active &&
                transaction.Date > afterDateExclusive &&
                transaction.Date < beforeDateExclusive &&
                !dbContext.DailyCloses.Any(close =>
                    close.BranchId == branchId &&
                    close.AccountId == accountId &&
                    close.Date == transaction.Date &&
                    close.Active &&
                    close.ItemsFirstRecordedAt != null))
            .OrderBy(transaction => transaction.Date)
            .Select(transaction => (DateTime?)transaction.Date)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsDraftByAccountAndDateAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime date,
        CancellationToken ct = default)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .AnyAsync(transaction =>
                transaction.BranchId == branchId &&
                transaction.AccountId == accountId &&
                transaction.Date == date &&
                transaction.Active &&
                transaction.Status == TransactionStatus.Draft,
                ct);
    }

    public async Task<IReadOnlyList<Transaction>> ListByBranchIdAndAccountIdAndDateRangeAsNoTracking(
        Guid branchId,
        DailyLedgerListFilter filter)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.AccountId == filter.AccountId &&
                t.Date >= filter.DateFrom &&
                t.Date <= filter.DateTo &&
                t.Active &&
                t.Status == TransactionStatus.Active)
            .Include(t => t.Account)
            .Include(t => t.Client)
            .Include(t => t.TransactionType)
            .Include(t => t.Category)
            .Include(t => t.RecordedByOperator)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
    }

    public async Task<int> CountByBranchIdAndAccountIdAndDateRangeAsNoTracking(
        Guid branchId,
        DailyLedgerListFilter filter)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .CountAsync(t =>
                t.BranchId == branchId &&
                t.AccountId == filter.AccountId &&
                t.Date >= filter.DateFrom &&
                t.Date <= filter.DateTo &&
                t.Active &&
                t.Status == TransactionStatus.Active);
    }

    public async Task<decimal> SumActiveByAccountAndDateBeforeAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime dateExclusive)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.AccountId == accountId &&
                t.Date < dateExclusive &&
                t.Active &&
                t.Status == TransactionStatus.Active)
            .Select(t => t.Direction == Direction.In ? t.Value : -(decimal?)t.Value)
            .SumAsync() ?? 0m;
    }

    public async Task<(decimal totalIn, decimal totalOut)> SumActiveByAccountAndDateRangeAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime dateFrom,
        DateTime dateTo)
    {
        var sums = await dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.AccountId == accountId &&
                t.Date >= dateFrom &&
                t.Date <= dateTo &&
                t.Active &&
                t.Status == TransactionStatus.Active)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                TotalIn = g.Where(t => t.Direction == Direction.In).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                TotalOut = g.Where(t => t.Direction == Direction.Out).Select(t => (decimal?)t.Value).Sum() ?? 0m
            })
            .FirstOrDefaultAsync();

        return sums is null ? (0m, 0m) : (sums.TotalIn, sums.TotalOut);
    }

    public async Task<IReadOnlyList<FiadoClientBalanceRow>> ListFiadoBalancesByBranchIdAsNoTracking(
        Guid branchId,
        Guid? clientId,
        DateTime asOfDate,
        CancellationToken ct = default)
    {
        var rows = await FiadoNettingSql
            .Query<FiadoBalanceSqlRow>(
                dbContext,
                FiadoBalancesCte + """
                SELECT b."ClientId", b."ClientName", b."OutstandingTotal"
                FROM balances b
                ORDER BY b."ClientName", b."ClientId"
                """,
                branchId,
                asOfDate,
                clientId)
            .ToListAsync(ct);

        return rows
            .Select(row => new FiadoClientBalanceRow(row.ClientId, row.ClientName, row.OutstandingTotal))
            .ToList();
    }

    public async Task<FiadoBalanceSummary> GetFiadoBalanceSummaryByBranchIdAsNoTracking(
        Guid branchId, DateTime asOfDate, CancellationToken ct = default)
    {
        var rows = await FiadoNettingSql
            .Query<FiadoBalanceSummarySqlRow>(
                dbContext,
                FiadoBalancesCte + """
                SELECT COUNT(*) FILTER (WHERE b."OutstandingTotal" > 0)::int AS "DebtorCount",
                       COALESCE(SUM(b."OutstandingTotal") FILTER (WHERE b."OutstandingTotal" > 0), 0) AS "TotalDebt",
                       COUNT(*) FILTER (WHERE b."OutstandingTotal" < 0)::int AS "CreditorCount",
                       COALESCE(-SUM(b."OutstandingTotal") FILTER (WHERE b."OutstandingTotal" < 0), 0) AS "TotalCredit"
                FROM balances b
                """,
                branchId,
                asOfDate,
                null)
            .ToListAsync(ct);

        var row = rows.Single();
        return new FiadoBalanceSummary(row.DebtorCount, row.TotalDebt, row.CreditorCount, row.TotalCredit);
    }

    public async Task<IReadOnlyList<FiadoClientBalanceRow>> ListTopFiadoDebtorsByBranchIdAsNoTracking(
        Guid branchId, DateTime asOfDate, int take, CancellationToken ct = default)
    {
        var rows = await FiadoNettingSql
            .Query<FiadoBalanceSqlRow>(
                dbContext,
                FiadoBalancesCte + """
                SELECT b."ClientId", b."ClientName", b."OutstandingTotal"
                FROM balances b
                WHERE b."OutstandingTotal" > 0
                ORDER BY b."OutstandingTotal" DESC, b."ClientName", b."ClientId"
                LIMIT {3}
                """,
                branchId,
                asOfDate,
                null,
                take)
            .ToListAsync(ct);

        return rows
            .Select(row => new FiadoClientBalanceRow(row.ClientId, row.ClientName, row.OutstandingTotal))
            .ToList();
    }

    // Per-client branch-wide balances over the shared netting projection; zero balances are omitted. The balance
    // report, the debt/credit split and the top debtors all compose this one CTE.
    private const string FiadoBalancesCte = """
        , balances AS (
            SELECT f."ClientId", c."Name" AS "ClientName", SUM(f."Signed") AS "OutstandingTotal"
            FROM fiado f
            INNER JOIN "Clients" c ON c."Id" = f."ClientId"
            GROUP BY f."ClientId", c."Name"
            HAVING SUM(f."Signed") <> 0
        )

        """;

    public async Task<IReadOnlyList<FiadoOpenSaleRow>> ListFiadoOpenSalesByBranchIdAsNoTracking(
        Guid branchId,
        Guid? accountId,
        Guid? clientId,
        DateTime asOfDate,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var rows = await FiadoNettingSql
            .Query<FiadoOpenSaleSqlRow>(
                dbContext,
                """
                SELECT o."Id", o."Date", o."DueDate", o."Remaining" AS "Value", o."ClientId", c."Name" AS "ClientName",
                       o."AccountId", a."Name" AS "AccountName", o."Description"
                FROM open_sales o
                INNER JOIN "Clients" c ON c."Id" = o."ClientId"
                INNER JOIN "Accounts" a ON a."Id" = o."AccountId"
                WHERE (CAST({3} AS uuid) IS NULL OR o."AccountId" = CAST({3} AS uuid))
                ORDER BY o."DueDate", o."Date", o."Id"
                LIMIT {4} OFFSET {5}
                """,
                branchId,
                asOfDate,
                clientId,
                accountId,
                pageSize,
                // long: a huge but valid page must not overflow into a negative OFFSET.
                (long)(page - 1) * pageSize)
            .ToListAsync(ct);

        return rows
            .Select(row => new FiadoOpenSaleRow(
                row.Id,
                row.Date,
                row.DueDate,
                row.Value,
                row.ClientId,
                row.ClientName,
                row.AccountId,
                row.AccountName,
                row.Description))
            .ToList();
    }

    public async Task<FiadoAgingTotals> GetFiadoAgingTotalsByBranchIdAsNoTracking(
        Guid branchId,
        Guid? accountId,
        Guid? clientId,
        DateTime asOfDate,
        CancellationToken ct = default)
    {
        var rows = await FiadoNettingSql
            .Query<FiadoAgingTotalSqlRow>(
                dbContext,
                """
                SELECT o."DueDate", SUM(o."Remaining") AS "Value", CAST(COUNT(*) AS integer) AS "RowCount",
                       MIN(o."Date") AS "OldestDate"
                FROM open_sales o
                WHERE (CAST({3} AS uuid) IS NULL OR o."AccountId" = CAST({3} AS uuid))
                GROUP BY o."DueDate"
                """,
                branchId,
                asOfDate,
                clientId,
                accountId)
            .ToListAsync(ct);

        return new FiadoAgingTotals(
            rows.Select(row => new AgingDueDateTotalRow(row.DueDate, row.Value, row.RowCount)).ToList(),
            rows.Count == 0 ? null : rows.Min(row => row.OldestDate));
    }

    public async Task<int> CountFiadoOpenSalesByBranchIdAsNoTracking(
        Guid branchId,
        Guid? accountId,
        Guid? clientId,
        DateTime asOfDate,
        CancellationToken ct = default)
    {
        var rows = await FiadoNettingSql
            .Query<FiadoCountSqlRow>(
                dbContext,
                """
                SELECT CAST(COUNT(*) AS integer) AS "Count"
                FROM open_sales o
                WHERE (CAST({3} AS uuid) IS NULL OR o."AccountId" = CAST({3} AS uuid))
                """,
                branchId,
                asOfDate,
                clientId,
                accountId)
            .ToListAsync(ct);

        return rows.Single().Count;
    }

    public async Task<IReadOnlyList<FiadoStatementRow>> ListFiadoStatementByClientIdAsNoTracking(
        Guid branchId,
        Guid clientId,
        DateTime? dateFrom,
        DateTime dateTo,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        var rows = await FiadoNettingSql
            .Query<FiadoStatementSqlRow>(
                dbContext,
                """
                , history AS (
                    SELECT f.*,
                           SUM(f."Signed") OVER (
                               ORDER BY f."Date", f."CreatedAt", f."Id"
                               ROWS BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW) AS "RunningBalance"
                    FROM fiado f
                )
                SELECT h."Id", h."Date", h."Direction", h."Value", h."AccountId", a."Name" AS "AccountName",
                       tt."Name" AS "TransactionTypeName", h."Description", h."RunningBalance"
                FROM history h
                INNER JOIN "Accounts" a ON a."Id" = h."AccountId"
                INNER JOIN "TransactionTypes" tt ON tt."Id" = h."TransactionTypeId"
                WHERE CAST({3} AS date) IS NULL OR h."Date" >= CAST({3} AS date)
                ORDER BY h."Date" DESC, h."CreatedAt" DESC, h."Id" DESC
                LIMIT {4} OFFSET {5}
                """,
                branchId,
                dateTo,
                clientId,
                dateFrom is { } from ? DateOnly.FromDateTime(from) : null,
                pageSize,
                // long: a huge but valid page must not overflow into a negative OFFSET.
                (long)(page - 1) * pageSize)
            .ToListAsync(ct);

        return rows
            .Select(row => new FiadoStatementRow(
                row.Id,
                row.Date,
                (Direction)row.Direction,
                row.Value,
                row.AccountId,
                row.AccountName,
                row.TransactionTypeName,
                row.Description,
                row.RunningBalance))
            .ToList();
    }

    public async Task<FiadoStatementWindow> GetFiadoStatementWindowByClientIdAsNoTracking(
        Guid branchId,
        Guid clientId,
        DateTime? dateFrom,
        DateTime dateTo,
        CancellationToken ct = default)
    {
        var rows = await FiadoNettingSql
            .Query<FiadoStatementWindowSqlRow>(
                dbContext,
                """
                SELECT COALESCE(SUM(f."Signed") FILTER (
                           WHERE CAST({3} AS date) IS NOT NULL AND f."Date" < CAST({3} AS date)), 0) AS "OpeningBalance",
                       COALESCE(SUM(f."Signed"), 0) AS "ClosingBalance",
                       CAST(COUNT(*) FILTER (
                           WHERE CAST({3} AS date) IS NULL OR f."Date" >= CAST({3} AS date)) AS integer) AS "TotalCount"
                FROM fiado f
                """,
                branchId,
                dateTo,
                clientId,
                dateFrom is { } from ? DateOnly.FromDateTime(from) : null)
            .ToListAsync(ct);

        var row = rows.Single();
        return new FiadoStatementWindow(row.OpeningBalance, row.ClosingBalance, row.TotalCount);
    }

    public async Task<FiadoClientPosition> GetFiadoClientPositionAsNoTracking(
        Guid branchId,
        Guid clientId,
        DateTime asOfDate,
        CancellationToken ct = default)
    {
        var rows = await FiadoNettingSql
            .Query<FiadoPositionSqlRow>(
                dbContext,
                """
                SELECT COALESCE((SELECT SUM(f."Signed") FROM fiado f), 0) AS "OutstandingTotal",
                       (SELECT MIN(o."Date") FROM open_sales o) AS "OldestUnpaidDate"
                """,
                branchId,
                asOfDate,
                clientId)
            .ToListAsync(ct);

        var row = rows.Single();
        return new FiadoClientPosition(row.OutstandingTotal, row.OldestUnpaidDate);
    }

    public async Task<OperatorTransactionSummaryProjection> SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
        Guid branchId,
        Guid operatorId,
        DateTime dateFrom,
        DateTime dateTo,
        IReadOnlyList<Guid>? allowedAccountIds = null)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.RecordedByOperatorId == operatorId &&
                t.Date >= dateFrom &&
                t.Date <= dateTo &&
                t.Active &&
                t.Status == TransactionStatus.Active);

        if (allowedAccountIds is not null)
        {
            query = query.Where(t => allowedAccountIds.Contains(t.AccountId));
        }

        var rawGroups = await query
            .GroupBy(t => new { t.CategoryId, t.Category.Name })
            .Select(g => new
            {
                g.Key.CategoryId,
                CategoryName = g.Key.Name,
                Count = g.Count(),
                TotalIn = g.Where(t => t.Direction == Direction.In).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                TotalOut = g.Where(t => t.Direction == Direction.Out).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                // M7.7 Phase 5.8: generated legs keep their initiating operator, so they are already counted above;
                // these sums only report how much of each total is automatic.
                GeneratedCount = g.Count(t => t.GenerationKind != null),
                GeneratedIn = g.Where(t => t.GenerationKind != null && t.Direction == Direction.In)
                    .Select(t => (decimal?)t.Value).Sum() ?? 0m,
                GeneratedOut = g.Where(t => t.GenerationKind != null && t.Direction == Direction.Out)
                    .Select(t => (decimal?)t.Value).Sum() ?? 0m
            })
            .ToListAsync();

        // Deterministic ordering applied on the projection (not in SQL) so the
        // contract follows .NET string comparison instead of database collation.
        var byCategory = rawGroups
            .Select(g => new CategoryTotal(
                g.CategoryId,
                g.CategoryName,
                g.Count,
                g.TotalIn,
                g.TotalOut,
                g.GeneratedCount,
                g.GeneratedIn,
                g.GeneratedOut))
            .OrderBy(c => c.CategoryName)
            .ThenBy(c => c.CategoryId)
            .ToList();

        return new OperatorTransactionSummaryProjection(
            byCategory.Sum(c => c.Count),
            byCategory.Sum(c => c.TotalIn),
            byCategory.Sum(c => c.TotalOut),
            byCategory,
            byCategory.Sum(c => c.GeneratedCount),
            byCategory.Sum(c => c.GeneratedIn),
            byCategory.Sum(c => c.GeneratedOut));
    }

    public async Task<IReadOnlyList<OpenChequeGroupRow>> ListOpenChequeGroupsByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, int page, int pageSize, CancellationToken ct = default)
    {
        if (PageOffset.TryGet(page, pageSize, out var offset) is false)
            return [];

        var baseQuery = BuildOpenChequeBaseQuery(branchId, accountId, clientId);

        // Group key: the plan's origin id, or a standalone cheque row's own id (M11 Phase 4.2). Both are transaction
        // ids and never collide, so ordering by the key keeps the shipped plan order and stays deterministic.
        var rawGroups = await baseQuery
            .GroupBy(t => t.OriginTransactionId ?? t.Id)
            .Where(g => g.Any(t => t.PaidAt == null))
            .Select(g => new
            {
                GroupKey = g.Key,
                PlanRowCount = g.Count(t => t.OriginTransactionId != null),
                OutstandingTotal = g.Where(t => t.PaidAt == null).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                OldestOpenDueDate = g.Where(t => t.PaidAt == null).Min(t => t.DueDate),
                OpenRowCount = g.Count(t => t.PaidAt == null),
                TotalRowCount = g.Count()
            })
            .OrderBy(g => g.OldestOpenDueDate)
            .ThenBy(g => g.GroupKey)
            .Skip(offset)
            .Take(pageSize)
            .ToListAsync(ct);

        if (rawGroups.Count == 0)
            return [];

        var planKeys = rawGroups.Where(g => g.PlanRowCount > 0).Select(g => g.GroupKey).ToList();
        var standaloneKeys = rawGroups.Where(g => g.PlanRowCount == 0).Select(g => g.GroupKey).ToList();
        var groupKeys = rawGroups.Select(g => g.GroupKey).ToList();

        // A plan group's identity comes from its origin row; a standalone group's from the row itself.
        var headRows = await dbContext.Transactions
            .AsNoTracking()
            .Include(t => t.Account)
            .Include(t => t.Client)
            .Where(t =>
                t.BranchId == branchId &&
                t.Active &&
                t.Status == TransactionStatus.Active &&
                groupKeys.Contains(t.Id))
            .ToListAsync(ct);

        var headById = headRows.ToDictionary(t => t.Id);

        // One batched read of the page's unpaid rows (replaces the former per-group sibling reads).
        var openRows = await dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.Active &&
                t.Status == TransactionStatus.Active &&
                t.PaidAt == null &&
                ((t.OriginTransactionId != null && planKeys.Contains(t.OriginTransactionId.Value)) ||
                 (t.OriginTransactionId == null && standaloneKeys.Contains(t.Id))))
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Id)
            .Select(t => new { Key = t.OriginTransactionId ?? t.Id, Row = new OpenChequeOpenRow(t.Id, t.DueDate, t.Value) })
            .ToListAsync(ct);

        var openRowsByKey = openRows
            .GroupBy(r => r.Key)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<OpenChequeOpenRow>)g.Select(r => r.Row).ToList());

        return rawGroups.Select(g =>
        {
            var head = headById.GetValueOrDefault(g.GroupKey);
            return new OpenChequeGroupRow(
                g.GroupKey,
                g.PlanRowCount > 0 ? g.GroupKey : null,
                g.OutstandingTotal,
                g.OldestOpenDueDate,
                g.OpenRowCount,
                g.TotalRowCount,
                head?.AccountId ?? Guid.Empty,
                head?.Account?.Name ?? string.Empty,
                head?.ClientId,
                head?.Client?.Name,
                head?.Description,
                openRowsByKey.GetValueOrDefault(g.GroupKey, []));
        }).ToList();
    }

    public async Task<IReadOnlyList<AgingDueDateTotalRow>> ListOpenChequeTotalsByDueDateAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, CancellationToken ct = default)
    {
        // An unpaid row always belongs to an open group, so the open rows are exactly the base query's unpaid rows.
        var rows = await BuildOpenChequeBaseQuery(branchId, accountId, clientId)
            .Where(t => t.PaidAt == null)
            .GroupBy(t => t.DueDate)
            .Select(g => new { DueDate = g.Key, Value = g.Sum(t => t.Value), RowCount = g.Count() })
            .ToListAsync(ct);

        return rows.Select(r => new AgingDueDateTotalRow(r.DueDate, r.Value, r.RowCount)).ToList();
    }

    public async Task<int> CountOpenChequeGroupsByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, CancellationToken ct = default)
    {
        return await BuildOpenChequeBaseQuery(branchId, accountId, clientId)
            .GroupBy(t => t.OriginTransactionId ?? t.Id)
            .Where(g => g.Any(t => t.PaidAt == null))
            .CountAsync(ct);
    }

    public async Task<IReadOnlyList<UpcomingSettlementRow>> ListUpcomingSettlementsByBranchIdAsNoTracking(
        Guid branchId, UpcomingSettlementFilter filter, int page, int pageSize, CancellationToken ct = default)
    {
        if (PageOffset.TryGet(page, pageSize, out var offset) is false)
            return [];

        return await BuildUpcomingSettlementQuery(branchId, filter)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Date)
            .ThenBy(t => t.Id)
            .Skip(offset)
            .Take(pageSize)
            .Select(t => new UpcomingSettlementRow(
                t.Id,
                t.OriginTransactionId,
                t.Date,
                t.DueDate,
                t.Value,
                t.TransactionType.SettlementInstrument,
                t.TransactionTypeId,
                t.TransactionType.Name,
                t.AccountId,
                t.Account.Name,
                t.ClientId,
                t.Client != null ? t.Client.Name : null))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UpcomingSettlementInstrumentTotalRow>> ListUpcomingSettlementTotalsByBranchIdAsNoTracking(
        Guid branchId, UpcomingSettlementFilter filter, CancellationToken ct = default)
    {
        var rows = await BuildUpcomingSettlementQuery(branchId, filter)
            .GroupBy(t => t.TransactionType.SettlementInstrument)
            .Select(g => new { Instrument = g.Key, Count = g.Count(), Value = g.Sum(t => t.Value) })
            .ToListAsync(ct);

        return rows.Select(r => new UpcomingSettlementInstrumentTotalRow(r.Instrument, r.Count, r.Value)).ToList();
    }

    public async Task<IReadOnlyList<MonthlyFinancialRoleRow>> ListMonthlyFinancialRoleRowsByBranchIdAsNoTracking(
        Guid branchId, MonthlyFinancialRole role, DateTime dateFrom, DateTime dateTo, CancellationToken ct = default)
    {
        var rows = await BuildMonthlyFinancialRoleQuery(branchId, role, dateFrom, dateTo)
            .GroupBy(t => new
            {
                t.Date,
                t.AccountId,
                AccountName = t.Account.Name,
                AccountActive = t.Account.Active,
                t.RecordedByOperatorId,
                OperatorName = t.RecordedByOperator.Name,
                OperatorActive = t.RecordedByOperator.Active,
                t.TransactionTypeId,
                TypeName = t.TransactionType.Name,
                TypeActive = t.TransactionType.Active
            })
            .Select(g => new
            {
                g.Key,
                InTotal = g.Where(t => t.Direction == Direction.In).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                OutTotal = g.Where(t => t.Direction == Direction.Out).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                RowCount = g.Count()
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new MonthlyFinancialRoleRow(
                r.Key.Date,
                r.Key.AccountId,
                r.Key.AccountName,
                r.Key.AccountActive,
                r.Key.RecordedByOperatorId,
                r.Key.OperatorName,
                r.Key.OperatorActive,
                r.Key.TransactionTypeId,
                r.Key.TypeName,
                r.Key.TypeActive,
                r.InTotal,
                r.OutTotal,
                r.RowCount))
            .ToList();
    }

    public async Task<ActivityPage<AccountActivityRow>> GetAccountActivityPageByBranchIdAndDateAsNoTracking(
        Guid branchId, DateTime date, int take, CancellationToken ct = default)
    {
        var listed = dbContext.Accounts
            .AsNoTracking()
            .Where(account =>
                account.BranchId == branchId &&
                ((account.Active && account.Type == AccountType.Terminal) ||
                 dbContext.Transactions.Any(t =>
                     t.AccountId == account.Id &&
                     t.BranchId == branchId &&
                     t.Active &&
                     t.Date == date &&
                     (t.Status == TransactionStatus.Active || t.Status == TransactionStatus.Draft))));

        var totalCount = await listed.CountAsync(ct);
        var accounts = await listed
            .OrderBy(account => account.Name)
            .ThenBy(account => account.Id)
            .Take(take)
            .Select(account => new { account.Id, account.Name, account.Type, account.Active })
            .ToListAsync(ct);
        var ids = accounts.Select(account => account.Id).ToList();

        var sums = await dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.Active &&
                t.Date == date &&
                (t.Status == TransactionStatus.Active || t.Status == TransactionStatus.Draft) &&
                ids.Contains(t.AccountId))
            .GroupBy(t => t.AccountId)
            .Select(g => new
            {
                AccountId = g.Key,
                TotalIn = g.Where(t => t.Status == TransactionStatus.Active && t.Direction == Direction.In)
                    .Select(t => (decimal?)t.Value).Sum() ?? 0m,
                TotalOut = g.Where(t => t.Status == TransactionStatus.Active && t.Direction == Direction.Out)
                    .Select(t => (decimal?)t.Value).Sum() ?? 0m,
                ActiveCount = g.Count(t => t.Status == TransactionStatus.Active),
                DraftCount = g.Count(t => t.Status == TransactionStatus.Draft)
            })
            .ToDictionaryAsync(row => row.AccountId, ct);

        return new ActivityPage<AccountActivityRow>(
            totalCount,
            accounts
                .Select(account =>
                {
                    var sum = sums.GetValueOrDefault(account.Id);
                    return new AccountActivityRow(
                        account.Id, account.Name, account.Type, account.Active,
                        sum?.TotalIn ?? 0m, sum?.TotalOut ?? 0m, sum?.ActiveCount ?? 0, sum?.DraftCount ?? 0);
                })
                .ToList());
    }

    public async Task<IReadOnlyList<AccountLastActivityRow>> ListLastActivityDatesByBranchIdAsNoTracking(
        Guid branchId, IReadOnlyCollection<Guid> accountIds, DateTime throughDate, CancellationToken ct = default)
    {
        if (accountIds.Count == 0)
            return [];

        var rows = await dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.Active &&
                t.Status == TransactionStatus.Active &&
                t.Date <= throughDate &&
                accountIds.Contains(t.AccountId))
            .GroupBy(t => t.AccountId)
            .Select(g => new { AccountId = g.Key, LastActivityDate = g.Max(t => t.Date) })
            .ToListAsync(ct);

        return rows.Select(r => new AccountLastActivityRow(r.AccountId, r.LastActivityDate)).ToList();
    }

    public async Task<ActivityPage<OperatorActivityRow>> GetOperatorActivityPageByBranchIdAndDateRangeAsNoTracking(
        Guid branchId, DateTime dateFrom, DateTime dateTo, int take, CancellationToken ct = default)
    {
        // Operator-summary predicates: Active rows recorded by the operator in the window.
        var recorded = dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.Date >= dateFrom &&
                t.Date <= dateTo &&
                t.Active &&
                t.Status == TransactionStatus.Active);

        var listed = dbContext.Operators
            .AsNoTracking()
            .Where(op => op.BranchId == branchId && (op.Active || recorded.Any(t => t.RecordedByOperatorId == op.Id)));

        var totalCount = await listed.CountAsync(ct);
        var operators = await listed
            .Select(op => new { op.Id, op.Name, op.Active, Count = recorded.Count(t => t.RecordedByOperatorId == op.Id) })
            .OrderByDescending(op => op.Count)
            .ThenBy(op => op.Name)
            .ThenBy(op => op.Id)
            .Take(take)
            .ToListAsync(ct);
        var ids = operators.Select(op => op.Id).ToList();

        var sums = await recorded
            .Where(t => ids.Contains(t.RecordedByOperatorId))
            .GroupBy(t => t.RecordedByOperatorId)
            .Select(g => new
            {
                OperatorId = g.Key,
                TotalIn = g.Where(t => t.Direction == Direction.In).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                TotalOut = g.Where(t => t.Direction == Direction.Out).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                GeneratedCount = g.Count(t => t.GenerationKind != null),
                GeneratedIn = g.Where(t => t.GenerationKind != null && t.Direction == Direction.In)
                    .Select(t => (decimal?)t.Value).Sum() ?? 0m,
                GeneratedOut = g.Where(t => t.GenerationKind != null && t.Direction == Direction.Out)
                    .Select(t => (decimal?)t.Value).Sum() ?? 0m
            })
            .ToDictionaryAsync(row => row.OperatorId, ct);

        return new ActivityPage<OperatorActivityRow>(
            totalCount,
            operators
                .Select(op =>
                {
                    var sum = sums.GetValueOrDefault(op.Id);
                    return new OperatorActivityRow(
                        op.Id, op.Name, op.Active, op.Count, sum?.TotalIn ?? 0m, sum?.TotalOut ?? 0m,
                        sum?.GeneratedCount ?? 0, sum?.GeneratedIn ?? 0m, sum?.GeneratedOut ?? 0m);
                })
                .ToList());
    }

    public async Task<MonthlyFinancialRoleTotal> GetMonthlyFinancialRoleTotalAsNoTracking(
        Guid branchId, MonthlyFinancialRole role, DateTime dateFrom, DateTime dateTo, CancellationToken ct = default)
    {
        var total = await BuildMonthlyFinancialRoleQuery(branchId, role, dateFrom, dateTo)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                InTotal = g.Where(t => t.Direction == Direction.In).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                OutTotal = g.Where(t => t.Direction == Direction.Out).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                RowCount = g.Count()
            })
            .SingleOrDefaultAsync(ct);

        return total is null ? new MonthlyFinancialRoleTotal(0m, 0m, 0) : new MonthlyFinancialRoleTotal(total.InTotal, total.OutTotal, total.RowCount);
    }

    // Monthly financial role rows (M11 Phase 6.1): Active rows of types carrying the role, active or retired, on Terminal
    // accounts in the window. The detailed rows and the totals compose this one predicate.
    private IQueryable<Transaction> BuildMonthlyFinancialRoleQuery(
        Guid branchId, MonthlyFinancialRole role, DateTime dateFrom, DateTime dateTo)
    {
        return dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.Active &&
                t.Status == TransactionStatus.Active &&
                t.Account.Type == AccountType.Terminal &&
                t.TransactionType.MonthlyFinancialRole == role &&
                t.Date >= dateFrom &&
                t.Date <= dateTo);
    }

    public async Task<IReadOnlyList<MonthlyTransactionCountRow>> CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
        Guid branchId, int year, int month, CancellationToken ct = default)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.Active &&
                t.Date.Year == year &&
                t.Date.Month == month)
            .GroupBy(t => new { t.Date.Date, t.Status })
            .OrderBy(g => g.Key.Date)
            .ThenBy(g => g.Key.Status)
            .Select(g => new MonthlyTransactionCountRow(g.Key.Date, g.Key.Status, g.Count()))
            .ToListAsync(ct);
    }

    public Task<int> CountDraftByBranchIdAndDateRangeAsNoTracking(
        Guid branchId,
        DateTime afterDateExclusive,
        DateTime throughDateInclusive,
        CancellationToken ct = default)
    {
        return dbContext.Transactions
            .AsNoTracking()
            .CountAsync(transaction =>
                transaction.BranchId == branchId &&
                transaction.Active &&
                transaction.Status == TransactionStatus.Draft &&
                transaction.Date > afterDateExclusive &&
                transaction.Date <= throughDateInclusive,
                ct);
    }

    public Task<DateTime?> GetEarliestDateByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default)
    {
        return dbContext.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.BranchId == branchId && transaction.Active)
            .Select(transaction => (DateTime?)transaction.Date)
            .MinAsync(ct);
    }

    public async Task<IReadOnlyList<TerminalActivityPairRow>> ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTracking(
        Guid branchId, int year, int month, CancellationToken ct = default)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(transaction =>
                transaction.BranchId == branchId &&
                transaction.Active &&
                transaction.Status == TransactionStatus.Active &&
                transaction.Account.Type == AccountType.Terminal &&
                transaction.Date.Year == year &&
                transaction.Date.Month == month)
            .GroupBy(transaction => new
            {
                transaction.Date.Date,
                transaction.AccountId,
                transaction.Account.Name
            })
            .OrderBy(group => group.Key.Date)
            .ThenBy(group => group.Key.Name)
            .ThenBy(group => group.Key.AccountId)
            .Select(group => new TerminalActivityPairRow(
                group.Key.Date,
                group.Key.AccountId,
                group.Key.Name))
            .ToListAsync(ct);
    }

    // Upcoming settlements (M11 Phase 5): the row's type is read whether or not it has since been retired, so a type's
    // current instrument classifies every row it ever recorded.
    private IQueryable<Transaction> BuildUpcomingSettlementQuery(Guid branchId, UpcomingSettlementFilter filter)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.Active &&
                t.Status == TransactionStatus.Active &&
                t.PaidAt == null &&
                t.TransactionType.SettlementInstrument != SettlementInstrument.None &&
                t.DueDate <= filter.DueDateTo);

        if (filter.DueDateFrom is { } dueDateFrom)
            query = query.Where(t => t.DueDate >= dueDateFrom);

        if (filter.AccountId is { } accountId)
            query = query.Where(t => t.AccountId == accountId);

        if (filter.Instrument is { } instrument)
            query = query.Where(t => t.TransactionType.SettlementInstrument == instrument);

        return query;
    }

    private IQueryable<Transaction> BuildOpenChequeBaseQuery(
        Guid branchId, Guid? accountId, Guid? clientId)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.Active &&
                t.Status == TransactionStatus.Active &&
                // Every installment plan row (shipped definition), plus a standalone row whose type — active or
                // retired — is classified Cheque (M11 Phase 4.2, working answer SQ2).
                (t.OriginTransactionId != null ||
                 t.TransactionType.SettlementInstrument == SettlementInstrument.Cheque));

        if (accountId is { } selectedAccountId)
            query = query.Where(t => t.AccountId == selectedAccountId);

        if (clientId is { } selectedClientId)
            query = query.Where(t => t.ClientId == selectedClientId);

        return query;
    }

    private static IQueryable<Transaction> ApplyFilter(
        IQueryable<Transaction> source,
        Guid branchId,
        TransactionListFilter filter)
    {
        var query = source.Where(transaction => transaction.BranchId == branchId);

        if (filter.AllowedAccountIds is not null)
        {
            query = query.Where(transaction => filter.AllowedAccountIds.Contains(transaction.AccountId));
        }

        if (filter.AccountId is { } accountId)
        {
            query = query.Where(transaction => transaction.AccountId == accountId);
        }

        if (filter.DateFrom is { } dateFrom)
        {
            query = query.Where(transaction => transaction.Date >= dateFrom);
        }

        if (filter.DateTo is { } dateTo)
        {
            query = query.Where(transaction => transaction.Date <= dateTo);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(transaction => transaction.Status == status);
        }

        if (filter.OperatorId is { } operatorId)
        {
            query = query.Where(transaction => transaction.RecordedByOperatorId == operatorId);
        }

        if (filter.ClientId is { } clientId)
        {
            query = query.Where(transaction => transaction.ClientId == clientId);
        }

        if (filter.OriginTransactionId is { } originTransactionId)
        {
            query = query.Where(transaction => transaction.OriginTransactionId == originTransactionId);
        }

        if (filter.TransactionTypeId is { } transactionTypeId)
        {
            query = query.Where(transaction => transaction.TransactionTypeId == transactionTypeId);
        }

        return query;
    }
}
