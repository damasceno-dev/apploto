using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace server.Infrastructure.Repositories;

internal class TimeEntriesRepository(ServerDbContext dbContext) : ITimeEntriesRepository
{
    public async Task Add(TimeEntry timeEntry)
    {
        await dbContext.TimeEntries.AddAsync(timeEntry);
    }

    public async Task<TimeEntry?> GetByIdAndBranchId(Guid id, Guid branchId)
    {
        return await dbContext.TimeEntries
            .Include(te => te.Operator)
            .Include(te => te.Branch)
            .Include(te => te.Segments
                .Where(segment => segment.Active)
                .OrderBy(segment => segment.ClockIn)
                .ThenBy(segment => segment.CreatedAt)
                .ThenBy(segment => segment.Id))
            .FirstOrDefaultAsync(te =>
                te.Id == id &&
                te.BranchId == branchId &&
                te.Active);
    }

    public async Task<TimeEntry?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.TimeEntries
            .AsNoTracking()
            .Include(te => te.Operator)
            .Include(te => te.Segments
                .Where(segment => segment.Active)
                .OrderBy(segment => segment.ClockIn)
                .ThenBy(segment => segment.CreatedAt)
                .ThenBy(segment => segment.Id))
            .FirstOrDefaultAsync(te =>
                te.Id == id &&
                te.BranchId == branchId &&
                te.Active);
    }

    public async Task<TimeEntry?> GetByBranchIdOperatorIdAndDate(Guid branchId, Guid operatorId, DateTime date)
    {
        return await dbContext.TimeEntries
            .Include(te => te.Segments
                .Where(segment => segment.Active)
                .OrderBy(segment => segment.ClockIn)
                .ThenBy(segment => segment.CreatedAt)
                .ThenBy(segment => segment.Id))
            .FirstOrDefaultAsync(te =>
                te.BranchId == branchId &&
                te.OperatorId == operatorId &&
                te.Date == date &&
                te.Active);
    }

    public async Task<IReadOnlyList<TimeEntry>> ListByBranchIdAsNoTracking(
        Guid branchId,
        TimeEntryListFilter filter)
    {
        var query = ApplyFilter(dbContext.TimeEntries, branchId, filter)
            .AsNoTracking()
            .Include(te => te.Operator)
            .Include(te => te.Segments
                .Where(segment => segment.Active)
                .OrderBy(segment => segment.ClockIn)
                .ThenBy(segment => segment.CreatedAt)
                .ThenBy(segment => segment.Id));

        var ordered = filter.InProgressFirst
            ? query
                .OrderByDescending(te => te.Segments.Any(segment => segment.Active && segment.ClockOut == null))
                .ThenByDescending(te => te.Date)
            : query.OrderByDescending(te => te.Date);

        return await ordered
            .ThenByDescending(te => te.CreatedAt)
            .ThenByDescending(te => te.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
    }

    public async Task<int> CountByBranchIdAsNoTracking(Guid branchId, TimeEntryListFilter filter)
    {
        return await ApplyFilter(dbContext.TimeEntries, branchId, filter)
            .AsNoTracking()
            .CountAsync();
    }

    public async Task<IReadOnlyList<TimeEntry>> ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
        Guid branchId,
        Guid operatorId,
        DateTime dateFrom,
        DateTime dateTo)
    {
        return await dbContext.TimeEntries
            .AsNoTracking()
            .Include(te => te.Segments.Where(segment => segment.Active))
            .Where(te =>
                te.BranchId == branchId &&
                te.OperatorId == operatorId &&
                te.Date >= dateFrom &&
                te.Date <= dateTo &&
                te.Active)
            .OrderBy(te => te.Date)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<TimeEntry>> ListByBranchIdAndDateRangeAsNoTracking(
        Guid branchId,
        DateTime dateFrom,
        DateTime dateTo)
    {
        return await dbContext.TimeEntries
            .AsNoTracking()
            .Include(te => te.Operator)
            .Include(te => te.Segments.Where(segment => segment.Active))
            .Where(te =>
                te.BranchId == branchId &&
                te.Date >= dateFrom &&
                te.Date <= dateTo &&
                te.Active)
            .OrderBy(te => te.Operator.Name)
            .ThenBy(te => te.Date)
            .ToListAsync();
    }

    public async Task<bool> ExistsActiveByBranchIdOperatorIdAndDate(Guid branchId, Guid operatorId, DateTime date)
    {
        return await dbContext.TimeEntries
            .AsNoTracking()
            .AnyAsync(te =>
                te.BranchId == branchId &&
                te.OperatorId == operatorId &&
                te.Date == date &&
                te.Active);
    }

    private static IQueryable<TimeEntry> ApplyFilter(
        IQueryable<TimeEntry> source,
        Guid branchId,
        TimeEntryListFilter filter)
    {
        var query = source.Where(te =>
            te.BranchId == branchId &&
            te.Active);

        if (filter.AllowedOperatorIds is not null)
        {
            query = query.Where(te => filter.AllowedOperatorIds.Contains(te.OperatorId));
        }

        if (filter.OperatorId is { } operatorId)
        {
            query = query.Where(te => te.OperatorId == operatorId);
        }

        if (filter.DateFrom is { } dateFrom)
        {
            query = query.Where(te => te.Date >= dateFrom);
        }

        if (filter.DateTo is { } dateTo)
        {
            query = query.Where(te => te.Date <= dateTo);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(te => te.Status == status);
        }

        if (filter.IsInProgress is { } isInProgress)
        {
            query = isInProgress
                ? query.Where(te => te.Segments.Any(segment => segment.Active && segment.ClockOut == null))
                : query.Where(te => !te.Segments.Any(segment => segment.Active && segment.ClockOut == null));
        }

        return query;
    }
}
