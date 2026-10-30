using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Domain.Models.Projections;

namespace server.Infrastructure.Repositories;

internal class DailyClosesRepository(ServerDbContext dbContext) : IDailyClosesRepository
{
    public async Task Add(DailyClose dailyClose, CancellationToken ct = default)
    {
        await dbContext.DailyCloses.AddAsync(dailyClose, ct);
    }

    public async Task<DailyClose?> GetByIdAndBranchId(Guid id, Guid branchId, CancellationToken ct = default)
    {
        return await IncludeRichResponse(dbContext.DailyCloses)
            .FirstOrDefaultAsync(dailyClose =>
                dailyClose.Id == id &&
                dailyClose.BranchId == branchId &&
                dailyClose.Active,
                ct);
    }

    public async Task<DailyClose?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default)
    {
        return await IncludeRichResponse(dbContext.DailyCloses)
            .AsNoTracking()
            .FirstOrDefaultAsync(dailyClose =>
                dailyClose.Id == id &&
                dailyClose.BranchId == branchId &&
                dailyClose.Active,
                ct);
    }

    public async Task<DailyClose?> GetCountedPredecessorAsNoTracking(DailyClose session, CancellationToken ct = default)
    {
        var query = dbContext.DailyCloses
            .AsNoTracking()
            .Include(dailyClose => dailyClose.Items)
            .Where(dailyClose =>
                dailyClose.BranchId == session.BranchId &&
                dailyClose.AccountId == session.AccountId &&
                dailyClose.Id != session.Id &&
                dailyClose.Active &&
                dailyClose.ItemsFirstRecordedAt != null);

        if (session.MappingState == SessionMappingState.Ready)
        {
            return await query
                .Where(dailyClose =>
                    dailyClose.MappingState == SessionMappingState.Ready &&
                    dailyClose.Sequence < session.Sequence)
                .OrderByDescending(dailyClose => dailyClose.Sequence)
                .FirstOrDefaultAsync(ct);
        }

        return await query
            .Where(dailyClose => dailyClose.Date < session.Date)
            .OrderByDescending(dailyClose => dailyClose.Date)
            .ThenByDescending(dailyClose => dailyClose.CreatedAt)
            .ThenByDescending(dailyClose => dailyClose.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<DailyClose?> GetDependentSuccessor(DailyClose session, CancellationToken ct = default)
    {
        if (session.MappingState != SessionMappingState.Ready)
            return null;

        // The first later session that either acknowledged an opening or recorded a count: its counted predecessor is
        // now this session, even when its acknowledged opening originally named another source (a past session
        // declared or moved between them, owner decision F2). An acknowledged successor gets a comparison revision,
        // an inherited one the opening recheck; a locked one refuses the originating correction.
        var successor = await IncludeRichResponse(dbContext.DailyCloses)
            .Where(dailyClose =>
                dailyClose.BranchId == session.BranchId &&
                dailyClose.AccountId == session.AccountId &&
                dailyClose.MappingState == SessionMappingState.Ready &&
                dailyClose.Active &&
                dailyClose.Sequence > session.Sequence &&
                (dailyClose.ItemsFirstRecordedAt != null || dailyClose.Opening != null))
            .OrderBy(dailyClose => dailyClose.Sequence)
            .FirstOrDefaultAsync(ct);

        return successor;
    }

    public Task<DailyClose?> GetActiveCustodyByAccountId(Guid branchId, Guid accountId, CancellationToken ct = default)
    {
        return IncludeRichResponse(dbContext.DailyCloses)
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.AccountId == accountId &&
                dailyClose.Active &&
                dailyClose.MappingState == SessionMappingState.Ready &&
                dailyClose.CustodyState == SessionCustodyState.Active)
            .FirstOrDefaultAsync(ct);
    }

    public Task<DailyClose?> GetActiveCustodyByAccountIdAsNoTracking(
        Guid branchId,
        Guid accountId,
        CancellationToken ct = default)
    {
        return IncludeRichResponse(dbContext.DailyCloses)
            .AsNoTracking()
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.AccountId == accountId &&
                dailyClose.Active &&
                dailyClose.MappingState == SessionMappingState.Ready &&
                dailyClose.CustodyState == SessionCustodyState.Active)
            .FirstOrDefaultAsync(ct);
    }

    public Task<DailyClose?> GetLatestCountedReadyByAccountIdAsNoTracking(
        Guid branchId,
        Guid accountId,
        CancellationToken ct = default)
    {
        return dbContext.DailyCloses
            .AsNoTracking()
            .Include(dailyClose => dailyClose.Items)
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.AccountId == accountId &&
                dailyClose.MappingState == SessionMappingState.Ready &&
                dailyClose.Active &&
                dailyClose.ItemsFirstRecordedAt != null)
            .OrderByDescending(dailyClose => dailyClose.Sequence)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> GetMaxSequenceByAccountId(Guid branchId, Guid accountId, CancellationToken ct = default)
    {
        return await dbContext.DailyCloses
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.AccountId == accountId &&
                dailyClose.MappingState == SessionMappingState.Ready)
            .MaxAsync(dailyClose => dailyClose.Sequence, ct) ?? 0;
    }

    public async Task<IReadOnlyList<DailyClose>> ListReadyByAccountIdOrderedByStart(
        Guid branchId,
        Guid accountId,
        CancellationToken ct = default)
    {
        return await IncludeRichResponse(dbContext.DailyCloses)
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.AccountId == accountId &&
                dailyClose.MappingState == SessionMappingState.Ready)
            .OrderBy(dailyClose => dailyClose.StartedAt)
            .ThenBy(dailyClose => dailyClose.Sequence)
            .ToListAsync(ct);
    }

    public async Task<bool> HasUnresolvedHistoryByAccountIdAsNoTracking(
        Guid branchId,
        Guid accountId,
        CancellationToken ct = default)
    {
        var unresolvedSession = await dbContext.DailyCloses
            .AsNoTracking()
            .AnyAsync(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.AccountId == accountId &&
                dailyClose.Active &&
                dailyClose.MappingState == SessionMappingState.Unresolved,
                ct);
        if (unresolvedSession)
            return true;

        return await dbContext.Transactions
            .AsNoTracking()
            .AnyAsync(transaction =>
                transaction.BranchId == branchId &&
                transaction.AccountId == accountId &&
                transaction.Active &&
                transaction.SessionId == null &&
                transaction.Account.Type == AccountType.Terminal,
                ct);
    }

    public async Task<DateTime?> GetEarliestUncountedPredecessorActivityDateAsNoTracking(
        DailyClose session,
        DateTime afterDateExclusive,
        CancellationToken ct = default)
    {
        return await dbContext.DailyCloses
            .AsNoTracking()
            .Where(dailyClose =>
                dailyClose.BranchId == session.BranchId &&
                dailyClose.AccountId == session.AccountId &&
                dailyClose.MappingState == SessionMappingState.Ready &&
                dailyClose.Active &&
                dailyClose.Sequence < session.Sequence &&
                dailyClose.Date > afterDateExclusive &&
                dailyClose.ItemsFirstRecordedAt == null &&
                dbContext.Transactions.Any(transaction =>
                    transaction.SessionId == dailyClose.Id &&
                    transaction.Active &&
                    transaction.Status == TransactionStatus.Active))
            .OrderBy(dailyClose => dailyClose.Sequence)
            .Select(dailyClose => (DateTime?)dailyClose.Date)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<DailyClose>> ListActiveCustodyByBranchIdAsNoTracking(
        Guid branchId,
        CancellationToken ct = default)
    {
        return await dbContext.DailyCloses
            .AsNoTracking()
            .Include(dailyClose => dailyClose.Account)
            .Include(dailyClose => dailyClose.CustodianOperator)
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.Active &&
                dailyClose.MappingState == SessionMappingState.Ready &&
                dailyClose.CustodyState == SessionCustodyState.Active)
            .OrderBy(dailyClose => dailyClose.Account.Name)
            .ThenBy(dailyClose => dailyClose.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DailyClose>> ListByBranchIdAndYearMonthAsNoTracking(
        Guid branchId, int year, int month, CancellationToken ct = default)
    {
        return await dbContext.DailyCloses
            .AsNoTracking()
            .Include(dailyClose => dailyClose.Account)
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.Active &&
                dailyClose.Date.Year == year &&
                dailyClose.Date.Month == month)
            .OrderBy(dailyClose => dailyClose.Date)
            .ThenBy(dailyClose => dailyClose.Account.Name)
            .ToListAsync(ct);
    }

    public Task<DateTime?> GetEarliestDateByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default)
    {
        return dbContext.DailyCloses
            .AsNoTracking()
            .Where(dailyClose => dailyClose.BranchId == branchId && dailyClose.Active)
            .Select(dailyClose => (DateTime?)dailyClose.Date)
            .MinAsync(ct);
    }

    public async Task<IReadOnlyList<ManagerQueueCloseRow>> ListManagerQueueClosesByBranchIdAndDateAsNoTracking(
        Guid branchId,
        DateTime date,
        CancellationToken ct = default)
    {
        return await dbContext.DailyCloses
            .AsNoTracking()
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.Date == date &&
                dailyClose.Active)
            .OrderBy(dailyClose => dailyClose.Account.Name)
            .ThenBy(dailyClose => dailyClose.Id)
            .Select(dailyClose => new ManagerQueueCloseRow(
                dailyClose.Id,
                dailyClose.AccountId,
                dailyClose.Account.Name,
                dailyClose.RecordedByUserId,
                dailyClose.RecordedByUser != null ? dailyClose.RecordedByUser.Name : null,
                dailyClose.RecordedByOperatorId,
                dailyClose.RecordedByOperator != null ? dailyClose.RecordedByOperator.Name : null,
                dailyClose.SubmittedByUserId,
                dailyClose.SubmittedByUser != null ? dailyClose.SubmittedByUser.Name : null,
                dailyClose.SubmittedByOperatorId,
                dailyClose.SubmittedByOperator != null ? dailyClose.SubmittedByOperator.Name : null,
                dailyClose.Status,
                dailyClose.SubmittedAt,
                dailyClose.ApprovedAt))
            .ToListAsync(ct);
    }

    public async Task<ManagerQueueBacklogRow> GetManagerQueueBacklogByBranchIdAndDateRangeAsNoTracking(
        Guid branchId,
        DateTime afterDateExclusive,
        DateTime beforeDateExclusive,
        CancellationToken ct = default)
    {
        // Pending statuses are listed explicitly so a future status never joins the backlog by default.
        var groups = await dbContext.DailyCloses
            .AsNoTracking()
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.Active &&
                dailyClose.Date > afterDateExclusive &&
                dailyClose.Date < beforeDateExclusive &&
                (dailyClose.Status == DailyCloseStatus.Submitted ||
                 dailyClose.Status == DailyCloseStatus.Rejected ||
                 dailyClose.Status == DailyCloseStatus.Draft))
            .GroupBy(dailyClose => dailyClose.Status)
            .Select(group => new
            {
                Status = group.Key,
                Count = group.Count(),
                OldestDate = group.Min(dailyClose => dailyClose.Date)
            })
            .ToListAsync(ct);

        return new ManagerQueueBacklogRow(
            groups.Where(group => group.Status == DailyCloseStatus.Submitted).Sum(group => group.Count),
            groups.Where(group => group.Status == DailyCloseStatus.Rejected).Sum(group => group.Count),
            groups.Where(group => group.Status == DailyCloseStatus.Draft).Sum(group => group.Count),
            groups.Count == 0 ? null : groups.Min(group => group.OldestDate));
    }

    public async Task<IReadOnlyList<DailyCloseSummaryRow>> ListCloseSummariesByBranchIdAndDateRangeAsNoTracking(
        Guid branchId, DateTime dateFrom, DateTime dateTo, int? take = null, CancellationToken ct = default)
    {
        var query = dbContext.DailyCloses
            .AsNoTracking()
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.Active &&
                dailyClose.Date >= dateFrom &&
                dailyClose.Date <= dateTo)
            .OrderByDescending(dailyClose => dailyClose.Date)
            .ThenByDescending(dailyClose => dailyClose.CreatedAt)
            .ThenByDescending(dailyClose => dailyClose.Id)
            .AsQueryable();

        if (take is { } limit)
            query = query.Take(limit);

        return await query
            .Select(dailyClose => new DailyCloseSummaryRow(
                dailyClose.Id,
                dailyClose.Date,
                dailyClose.CreatedAt,
                dailyClose.AccountId,
                dailyClose.Account.Name,
                dailyClose.Account.Active,
                dailyClose.Status,
                dailyClose.RecordedByUserId,
                dailyClose.RecordedByUser != null ? dailyClose.RecordedByUser.Name : null,
                dailyClose.RecordedByOperatorId,
                dailyClose.RecordedByOperator != null ? dailyClose.RecordedByOperator.Name : null,
                dailyClose.SubmittedByUserId,
                dailyClose.SubmittedByUser != null ? dailyClose.SubmittedByUser.Name : null,
                dailyClose.SubmittedByOperatorId,
                dailyClose.SubmittedByOperator != null ? dailyClose.SubmittedByOperator.Name : null))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DailyCloseStatusCountRow>> CountByStatusByBranchIdAndDateRangeAsNoTracking(
        Guid branchId, DateTime dateFrom, DateTime dateTo, CancellationToken ct = default)
    {
        var rows = await dbContext.DailyCloses
            .AsNoTracking()
            .Where(dailyClose =>
                dailyClose.BranchId == branchId &&
                dailyClose.Active &&
                dailyClose.Date >= dateFrom &&
                dailyClose.Date <= dateTo)
            .GroupBy(dailyClose => dailyClose.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        return rows.Select(row => new DailyCloseStatusCountRow(row.Status, row.Count)).ToList();
    }

    public async Task<IReadOnlyList<DailyClose>> ListByBranchIdAsNoTracking(
        Guid branchId,
        DailyCloseListFilter filter,
        CancellationToken ct = default)
    {
        return await ApplyFilter(dbContext.DailyCloses, branchId, filter)
            .AsNoTracking()
            .Include(dailyClose => dailyClose.Account)
            .Include(dailyClose => dailyClose.RecordedByUser)
            .Include(dailyClose => dailyClose.RecordedByOperator)
            .Include(dailyClose => dailyClose.SubmittedByUser)
            .Include(dailyClose => dailyClose.SubmittedByOperator)
            .Include(dailyClose => dailyClose.ApprovedByUser)
            .Include(dailyClose => dailyClose.CustodianOperator)
            .Include(dailyClose => dailyClose.Opening)
            .OrderByDescending(dailyClose => dailyClose.Date)
            .ThenByDescending(dailyClose => dailyClose.CreatedAt)
            .ThenByDescending(dailyClose => dailyClose.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountByBranchIdAsNoTracking(
        Guid branchId, DailyCloseListFilter filter, CancellationToken ct = default)
    {
        return await ApplyFilter(dbContext.DailyCloses, branchId, filter)
            .AsNoTracking()
            .CountAsync(ct);
    }

    private static IQueryable<DailyClose> IncludeRichResponse(IQueryable<DailyClose> source)
    {
        return source
            .Include(dailyClose => dailyClose.Account)
            .Include(dailyClose => dailyClose.OpenedByUser)
            .Include(dailyClose => dailyClose.RecordedByUser)
            .Include(dailyClose => dailyClose.RecordedByOperator)
            .Include(dailyClose => dailyClose.SubmittedByUser)
            .Include(dailyClose => dailyClose.SubmittedByOperator)
            .Include(dailyClose => dailyClose.ApprovedByUser)
            .Include(dailyClose => dailyClose.OpeningRecheckTriggeredByDailyClose)
            .Include(dailyClose => dailyClose.OpeningRecheckTriggeredByUser)
            .Include(dailyClose => dailyClose.CustodianOperator)
            .Include(dailyClose => dailyClose.Opening)
            .ThenInclude(opening => opening!.Items)
            .Include(dailyClose => dailyClose.Items)
            .ThenInclude(item => item.Product);
    }

    private static IQueryable<DailyClose> ApplyFilter(IQueryable<DailyClose> source,Guid branchId,DailyCloseListFilter filter)
    {
        var query = source.Where(dailyClose =>
            dailyClose.BranchId == branchId &&
            dailyClose.Active);

        if (filter.AllowedAccountIds is not null)
            query = query.Where(dailyClose => filter.AllowedAccountIds.Contains(dailyClose.AccountId));

        if (filter.AccountId is { } accountId)
            query = query.Where(dailyClose => dailyClose.AccountId == accountId);

        if (filter.Status is { } status)
            query = query.Where(dailyClose => dailyClose.Status == status);

        if (filter.DateFrom is { } dateFrom)
            query = query.Where(dailyClose => dailyClose.Date >= dateFrom);

        if (filter.DateTo is { } dateTo)
            query = query.Where(dailyClose => dailyClose.Date <= dateTo);

        if (filter.OperatorId is { } operatorId)
            query = query.Where(dailyClose => dailyClose.RecordedByOperatorId == operatorId);

        if (filter.CustodianOperatorId is { } custodianOperatorId)
            query = query.Where(dailyClose => dailyClose.CustodianOperatorId == custodianOperatorId);

        if (filter.CustodyState is { } custodyState)
            query = query.Where(dailyClose => dailyClose.CustodyState == custodyState);

        if (filter.TerminalId is { } terminalId)
            query = query.Where(dailyClose => dailyClose.TerminalId == terminalId);

        return query;
    }
}
