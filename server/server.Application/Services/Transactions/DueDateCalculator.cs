using server.Domain.Entities.Enums;

namespace server.Application.Services.Transactions;

public class DueDateCalculator
{
    public static DateTime Compute(
        SettlementRule rule,
        DateTime date,
        DateTime? operatorProvidedDueDate,
        IReadOnlySet<DateOnly>? holidayDates = null)
    {
        return rule switch
        {
            SettlementRule.SameDay => date,
            SettlementRule.NextCalendarDay => date.AddDays(1),
            SettlementRule.NextBusinessDay => AddBusinessDays(date, 1, holidayDates),
            SettlementRule.TwoBusinessDays => AddBusinessDays(date, 2, holidayDates),
            SettlementRule.OperatorEnteredCheque => operatorProvidedDueDate
                ?? throw new ArgumentException("OperatorEnteredCheque requires an explicit DueDate"),
            _ => date
        };
    }

    public static DateTime AdjustToNextBusinessDay(DateTime date, IReadOnlySet<DateOnly>? holidayDates = null)
    {
        var result = date;

        while (result.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
               || IsHoliday(result, holidayDates))
        {
            result = result.AddDays(1);
        }

        return result;
    }

    private static DateTime AddBusinessDays(DateTime date, int businessDays, IReadOnlySet<DateOnly>? holidayDates)
    {
        var result = date;

        for (var addedDays = 0; addedDays < businessDays;)
        {
            result = result.AddDays(1);

            if (result.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                || IsHoliday(result, holidayDates))
            {
                continue;
            }

            addedDays++;
        }

        return result;
    }

    private static bool IsHoliday(DateTime date, IReadOnlySet<DateOnly>? holidayDates)
    {
        return holidayDates is not null && holidayDates.Contains(DateOnly.FromDateTime(date));
    }
}
