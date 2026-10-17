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
                """
                SELECT f."ClientId", c."Name" AS "ClientName", SUM(f."Signed") AS "OutstandingTotal"
                FROM fiado f
                INNER JOIN "Clients" c ON c."Id" = f."ClientId"
                GROUP BY f."ClientId", c."Name"
                HAVING SUM(f."Signed") <> 0
                ORDER BY c."Name", f."ClientId"
                """,
                branchId,
                asOfDate,
                clientId)
            .ToListAsync(ct);

        return rows
            .Select(row => new FiadoClientBalanceRow(row.ClientId, row.ClientName, row.OutstandingTotal))
            .ToList();
    }

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
        Guid branchId, Guid? accountId, Guid? clientId, int page, int pageSize)
    {
        var baseQuery = BuildOpenChequeBaseQuery(branchId, accountId, clientId);

        var rawGroups = await baseQuery
            .GroupBy(t => t.OriginTransactionId!.Value)
            .Where(g => g.Any(t => t.PaidAt == null))
            .Select(g => new
            {
                OriginTransactionId = g.Key,
                OutstandingTotal = g.Where(t => t.PaidAt == null).Select(t => (decimal?)t.Value).Sum() ?? 0m,
                OldestOpenDueDate = g.Where(t => t.PaidAt == null).Min(t => t.DueDate),
                OpenRowCount = g.Count(t => t.PaidAt == null),
                TotalRowCount = g.Count()
            })
            .OrderBy(g => g.OldestOpenDueDate)
            .ThenBy(g => g.OriginTransactionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (rawGroups.Count == 0)
            return [];

        var originIds = rawGroups.Select(g => g.OriginTransactionId).ToList();
        var originRows = await dbContext.Transactions
            .AsNoTracking()
            .Include(t => t.Account)
            .Include(t => t.Client)
            .Where(t =>
                t.BranchId == branchId &&
                t.Active &&
                t.Status == TransactionStatus.Active &&
                originIds.Contains(t.Id))
            .ToListAsync();

        var originDict = originRows.ToDictionary(t => t.Id);

        return rawGroups.Select(g =>
        {
            var origin = originDict.GetValueOrDefault(g.OriginTransactionId);
            return new OpenChequeGroupRow(
                g.OriginTransactionId,
                g.OutstandingTotal,
                g.OldestOpenDueDate,
                g.OpenRowCount,
                g.TotalRowCount,
                origin?.AccountId ?? Guid.Empty,
                origin?.Account?.Name ?? string.Empty,
                origin?.ClientId,
                origin?.Client?.Name,
                origin?.Description);
        }).ToList();
    }

    public async Task<int> CountOpenChequeGroupsByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId)
    {
        return await BuildOpenChequeBaseQuery(branchId, accountId, clientId)
            .GroupBy(t => t.OriginTransactionId!.Value)
            .Where(g => g.Any(t => t.PaidAt == null))
            .CountAsync();
    }

    public async Task<IReadOnlyList<Transaction>> ListActiveByOriginTransactionIdAndBranchIdAsNoTracking(
        Guid originId, Guid branchId)
    {
        return await dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.OriginTransactionId == originId &&
                t.BranchId == branchId &&
                t.Active &&
                t.Status == TransactionStatus.Active)
            .OrderBy(t => t.DueDate)
            .ThenBy(t => t.Id)
            .ToListAsync();
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

    private IQueryable<Transaction> BuildOpenChequeBaseQuery(
        Guid branchId, Guid? accountId, Guid? clientId)
    {
        var query = dbContext.Transactions
            .AsNoTracking()
            .Where(t =>
                t.BranchId == branchId &&
                t.Active &&
                t.Status == TransactionStatus.Active &&
                t.OriginTransactionId != null);

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
