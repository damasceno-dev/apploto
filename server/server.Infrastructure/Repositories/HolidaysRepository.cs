using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace server.Infrastructure.Repositories;

internal class HolidaysRepository(ServerDbContext dbContext) : IHolidaysRepository
{
    public async Task Add(Holiday holiday)
    {
        await dbContext.Holidays.AddAsync(holiday);
    }

    public async Task<Holiday?> GetByIdAndBranchId(Guid id, Guid branchId)
    {
        return await dbContext.Holidays
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                h.BranchId == branchId &&
                h.Active);
    }

    public async Task<Holiday?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId)
    {
        return await dbContext.Holidays
            .AsNoTracking()
            .FirstOrDefaultAsync(h =>
                h.Id == id &&
                h.BranchId == branchId &&
                h.Active);
    }

    public async Task<Holiday?> GetActiveByBranchIdAndDate(Guid branchId, DateTime date)
    {
        return await dbContext.Holidays
            .AsNoTracking()
            .FirstOrDefaultAsync(h =>
                h.BranchId == branchId &&
                h.Date == date &&
                h.Active);
    }

    public async Task<IReadOnlyList<Holiday>> ListByBranchIdAsNoTracking(
        Guid branchId,
        HolidayListFilter filter)
    {
        return await ApplyFilter(dbContext.Holidays, branchId, filter)
            .AsNoTracking()
            .OrderByDescending(h => h.Date)
            .ThenByDescending(h => h.CreatedAt)
            .ThenByDescending(h => h.Id)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
    }

    public async Task<int> CountByBranchIdAsNoTracking(Guid branchId, HolidayListFilter filter)
    {
        return await ApplyFilter(dbContext.Holidays, branchId, filter)
            .AsNoTracking()
            .CountAsync();
    }

    public async Task<IReadOnlyList<DateOnly>> ListActiveDatesByBranchIdAsNoTracking(
        Guid branchId,
        CancellationToken ct = default)
    {
        var dates = await dbContext.Holidays
            .AsNoTracking()
            .Where(h => h.BranchId == branchId && h.Active)
            .Select(h => h.Date)
            .ToListAsync(ct);

        return dates.Select(DateOnly.FromDateTime).ToList();
    }

    public async Task<IReadOnlyList<DateOnly>> ListActiveDatesByBranchIdAndYearAsNoTracking(Guid branchId, int year)
    {
        var start = new DateTime(year, 1, 1);
        var end = start.AddYears(1);

        var dates = await dbContext.Holidays
            .AsNoTracking()
            .Where(h =>
                h.BranchId == branchId &&
                h.Active &&
                h.Date >= start &&
                h.Date < end)
            .Select(h => h.Date)
            .ToListAsync();

        return dates.Select(DateOnly.FromDateTime).ToList();
    }

    private static IQueryable<Holiday> ApplyFilter(
        IQueryable<Holiday> source,
        Guid branchId,
        HolidayListFilter filter)
    {
        var query = source.Where(h =>
            h.BranchId == branchId &&
            h.Active);

        if (filter.Year is { } year)
        {
            query = query.Where(h => h.Date.Year == year);
        }

        if (filter.DateFrom is { } dateFrom)
        {
            query = query.Where(h => h.Date >= dateFrom);
        }

        if (filter.DateTo is { } dateTo)
        {
            query = query.Where(h => h.Date <= dateTo);
        }

        return query;
    }
}
