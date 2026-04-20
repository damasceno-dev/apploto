using server.Domain.Entities.Enums;

namespace server.Application.Services.Transactions;

public class DueDateCalculator
{
    public DateTime Compute(SettlementRule rule, DateTime date, DateTime? operatorProvidedDueDate)
    {
        return rule switch
        {
            SettlementRule.SameDay => date,
            SettlementRule.NextCalendarDay => date.AddDays(1),
            SettlementRule.NextBusinessDay => AddBusinessDays(date, 1),
            SettlementRule.TwoBusinessDays => AddBusinessDays(date, 2),
            SettlementRule.OperatorEnteredCheque => operatorProvidedDueDate
                ?? throw new ArgumentException("OperatorEnteredCheque requires an explicit DueDate"),
            _ => date
        };
    }

    private static DateTime AddBusinessDays(DateTime date, int businessDays)
    {
        var result = date;

        for (var addedDays = 0; addedDays < businessDays;)
        {
            result = result.AddDays(1);

            if (result.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            addedDays++;
        }

        return result;
    }
}
