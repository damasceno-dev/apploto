namespace server.Domain.Entities.Enums;

public enum SettlementRule
{
    SameDay = 0,
    NextCalendarDay = 1,
    NextBusinessDay = 2,
    TwoBusinessDays = 3,
    OperatorEnteredCheque = 4
}
