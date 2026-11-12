using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.TimeEntries;

/// <summary>Shared current attendance invariants for ordinary writes and reviewed historical imports.</summary>
public static class TimeEntryRules
{
    public static IEnumerable<TimeEntrySegment> ActiveSegments(TimeEntry entry) => entry.Segments.Where(x => x.Active).OrderBy(x => x.ClockIn).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id);

    public static void EnsureSegments(TimeEntry entry)
    {
        var segments = ActiveSegments(entry).ToList();
        var dayStart = entry.Date.Date;
        var dayEnd = dayStart.AddDays(1);

        foreach (var segment in segments)
        {
            if (segment.ClockIn == default)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_IN_REQUIRED]);

            if (segment.ClockIn < dayStart || segment.ClockIn >= dayEnd)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS]);

            if (segment.ClockOut is not { } clockOut)
                continue;

            if (clockOut <= segment.ClockIn)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_CLOCK_OUT_BEFORE_CLOCK_IN]);

            if (clockOut - segment.ClockIn > TimeSpan.FromHours(24))
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENT_OUT_OF_DAY_BOUNDS]);
        }

        if (segments.Count(segment => segment.ClockOut is null) > 1)
            throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS]);

        for (var index = 0; index < segments.Count - 1; index++)
        {
            var current = segments[index];
            var next = segments[index + 1];

            if (current.ClockOut is null)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_MULTIPLE_OPEN_SEGMENTS]);

            if (current.ClockOut > next.ClockIn)
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SEGMENTS_OVERLAP]);
        }
    }
    public static async Task EnsureCalendar(TimeEntryStatus status, DateTime date, Guid branchId, IHolidaysRepository holidaysRepository)
    {
        switch (status)
        {
            case TimeEntryStatus.Sunday when date.DayOfWeek is not DayOfWeek.Sunday:
                throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_SUNDAY_REQUIRES_SUNDAY_DATE]);
            case TimeEntryStatus.Holiday:
                {
                    var holiday = await holidaysRepository.GetActiveByBranchIdAndDate(branchId, date);
                    if (holiday is null)
                        throw new OnValidationException([ResourcesErrorMessages.TIMEENTRY_HOLIDAY_REQUIRES_ACTIVE_HOLIDAY]);

                    break;
                }
            case TimeEntryStatus.Sunday:
            case TimeEntryStatus.Present:
            case TimeEntryStatus.DayOff:
            case TimeEntryStatus.Vacation:
            case TimeEntryStatus.JustifiedAbsence:
            case TimeEntryStatus.UnjustifiedAbsence:
                break;
            default:
                throw new InvalidOperationException($"Unexpected {nameof(TimeEntryStatus)}: {status}");
        }
    }
}
