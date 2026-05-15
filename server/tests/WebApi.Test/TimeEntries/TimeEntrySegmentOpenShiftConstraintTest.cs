using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TimeEntries;

/// <summary>
/// Verifies the hard PostgreSQL uniqueness enforcement for the filtered
/// <c>UNIQUE (TimeEntryId) WHERE "ClockOut" IS NULL AND "Active" = true</c> index on
/// <c>TimeEntrySegments</c> by attempting a raw duplicate open-segment insert directly
/// through <see cref="ServerDbContext"/> — bypassing all application-layer pre-checks.
///
/// This test is intentionally separate from the HTTP-level 409 tests that Slice 3.6.B will
/// add: those verify the application-layer duplicate check that runs before <c>Commit()</c>.
/// This test proves that the underlying migration correctly applied the filtered index so
/// that even a concurrent race that slips past the pre-check is caught by the database.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TimeEntrySegmentOpenShiftConstraintTest(ServerWebApplicationFactory factory)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    [Fact]
    public async Task TimeEntrySegment_ShouldRejectSecondOpenSegmentForSameTimeEntry_AtDatabaseLevel()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("SegmentOpenShiftConstraint");

        using var setupScope = factory.Services.CreateScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var op = new Operator
        {
            Id = Guid.NewGuid(),
            Name = "Constraint Test Operator",
            BranchId = branch.Id
        };
        setupDb.Operators.Add(op);

        var today = TodayUnspecified();
        var timeEntry = new TimeEntry
        {
            Id = Guid.NewGuid(),
            Date = today,
            Status = TimeEntryStatus.Present,
            TotalHours = 0m,
            BalanceHours = 0m,
            OperatorId = op.Id,
            BranchId = branch.Id
        };
        setupDb.TimeEntries.Add(timeEntry);

        var firstSegment = new TimeEntrySegment
        {
            Id = Guid.NewGuid(),
            TimeEntryId = timeEntry.Id,
            ClockIn = today.AddHours(8),
            ClockOut = null
        };
        setupDb.TimeEntrySegments.Add(firstSegment);
        await setupDb.SaveChangesAsync();

        // Attempt a second open segment for the same TimeEntryId, bypassing the
        // application-layer check. If the filtered unique index was applied by the
        // migration, SaveChangesAsync must reject it.
        using var conflictScope = factory.Services.CreateScope();
        var conflictDb = conflictScope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var secondOpenSegment = new TimeEntrySegment
        {
            Id = Guid.NewGuid(),
            TimeEntryId = timeEntry.Id,
            ClockIn = today.AddHours(10),
            ClockOut = null
        };
        conflictDb.TimeEntrySegments.Add(secondOpenSegment);

        var exception = await Should.ThrowAsync<DbUpdateException>(() => conflictDb.SaveChangesAsync());
        var postgresException = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresException.SqlState.ShouldBe(PostgresUniqueViolationSqlState);
        postgresException.ConstraintName.ShouldBe("IX_TimeEntrySegments_TimeEntryId_OpenShift");
    }

    [Fact]
    public async Task TimeEntrySegment_ShouldAllowMultipleClosedSegments_ForSameTimeEntry_AtDatabaseLevel()
    {
        // The filtered index only applies WHERE ClockOut IS NULL. Multiple closed segments
        // (ClockOut set) for the same TimeEntryId must be insertable without conflict.
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("SegmentClosedMultiple");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var op = new Operator
        {
            Id = Guid.NewGuid(),
            Name = "Closed Segments Operator",
            BranchId = branch.Id
        };
        dbContext.Operators.Add(op);

        var today = TodayUnspecified();
        var timeEntry = new TimeEntry
        {
            Id = Guid.NewGuid(),
            Date = today,
            Status = TimeEntryStatus.Present,
            TotalHours = 0m,
            BalanceHours = 0m,
            OperatorId = op.Id,
            BranchId = branch.Id
        };
        dbContext.TimeEntries.Add(timeEntry);

        dbContext.TimeEntrySegments.Add(new TimeEntrySegment
        {
            Id = Guid.NewGuid(),
            TimeEntryId = timeEntry.Id,
            ClockIn = today.AddHours(8),
            ClockOut = today.AddHours(12)
        });
        dbContext.TimeEntrySegments.Add(new TimeEntrySegment
        {
            Id = Guid.NewGuid(),
            TimeEntryId = timeEntry.Id,
            ClockIn = today.AddHours(13),
            ClockOut = today.AddHours(17)
        });

        // Should NOT throw — closed segments are excluded from the open-shift unique index.
        await Should.NotThrowAsync(() => dbContext.SaveChangesAsync());
    }

    private static DateTime TodayUnspecified()
    {
        return DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
    }
}
