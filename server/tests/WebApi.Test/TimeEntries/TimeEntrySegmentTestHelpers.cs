using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using WebApi.Test.Infrastructure;

namespace WebApi.Test.TimeEntries;

internal static class TimeEntrySegmentTestHelpers
{
    public static async Task<TimeEntry> SeedTimeEntryWithSegmentsAsync(
        ServerWebApplicationFactory factory,
        Guid branchId,
        Guid operatorId,
        DateTime date,
        TimeEntryStatus status = TimeEntryStatus.Present,
        params (DateTime ClockIn, DateTime? ClockOut, bool Active)[] segments)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var entry = new TimeEntry
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
            Date = date,
            Status = status,
            TotalHours = 0m,
            BalanceHours = 0m,
            OperatorId = operatorId,
            BranchId = branchId
        };
        dbContext.TimeEntries.Add(entry);

        foreach (var segment in segments)
        {
            dbContext.TimeEntrySegments.Add(new TimeEntrySegment
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow,
                TimeEntryId = entry.Id,
                ClockIn = segment.ClockIn,
                ClockOut = segment.ClockOut,
                Active = segment.Active
            });
        }

        await dbContext.SaveChangesAsync();
        return entry;
    }

    public static async Task<TimeEntry> ReloadTimeEntryWithSegmentsAsync(
        ServerWebApplicationFactory factory,
        Guid timeEntryId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        return await dbContext.TimeEntries
            .AsNoTracking()
            .Include(entry => entry.Segments)
            .SingleAsync(entry => entry.Id == timeEntryId);
    }

    public static async Task<TimeEntrySegment> ReloadSegmentAsync(
        ServerWebApplicationFactory factory,
        Guid segmentId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        return await dbContext.TimeEntrySegments
            .AsNoTracking()
            .SingleAsync(segment => segment.Id == segmentId);
    }

    public static DateTime FixedDate()
    {
        return new DateTime(2026, 5, 8);
    }
}
