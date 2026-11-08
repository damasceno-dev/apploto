using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using NSubstitute;
using server.Application.Services.Reports;
using server.Application.Services.TimeEntries;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
namespace CommonTestUtilities.Services;

public class EmployeeHourBankScenarioBuilder
{
    public Operator Person { get; } = new OperatorBuilder().Build();
    public IEmployeeRecordsRepository Records { get; } = Substitute.For<IEmployeeRecordsRepository>();
    public ITimeEntriesRepository Attendance { get; } = Substitute.For<ITimeEntriesRepository>();
    private IReadOnlyList<HourBankEntry> ledger = [];
    private IReadOnlyList<TimeEntry> days = [];
    public EmployeeHourBankScenarioBuilder WithLedger(params HourBankEntry[] rows) { ledger = rows; return this; }
    public EmployeeHourBankScenarioBuilder WithAttendance(params TimeEntry[] rows) { days = rows; return this; }
    public HourBankEntry Entry(
        HourBankEntryKind kind,
        decimal value,
        DateTime source,
        DateTime? posted = null,
        Guid? previous = null,
        decimal? contribution = null) => new()
    {
        OperatorId = Person.Id,
        BranchId = Person.BranchId,
        Kind = kind,
        SignedHours = value,
        SignedContribution = contribution ?? value,
        SourceDate = source,
        PostedOn = posted ?? source,
        PreviousId = previous,
        Reason = "Explicit synthetic evidence",
        Evidence = "No assumed history"
    };
    public TimeEntry Day(DateTime date, TimeEntryStatus status = TimeEntryStatus.DayOff, decimal? hours = null)
    {
        var id = Guid.NewGuid(); var builder = new TimeEntryBuilder().WithId(id).WithOperator(Person).WithDate(date).WithStatus(status);
        if (status == TimeEntryStatus.Present)
            builder.WithSegment(new TimeEntrySegmentBuilder().WithTimeEntryId(id).WithClockIn(date.AddHours(8))
                .WithClockOut(hours is { } worked ? date.AddHours(8 + (double)worked) : null).Build());
        return builder.Build();
    }
    public EmployeeHourBankReader Build()
    {
        Records.List<HourBankEntry>(Person.BranchId, Arg.Any<CancellationToken>()).Returns(ledger);
        Attendance.ListByBranchIdAndOperatorIdAndDateRangeAsNoTracking(Person.BranchId, Person.Id, Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(call => (IReadOnlyList<TimeEntry>)days.Where(d => d.Date >= call.ArgAt<DateTime>(2) && d.Date <= call.ArgAt<DateTime>(3)).ToList());
        var policies = new TimeEntryPoliciesRepositoryBuilder().ListActiveByBranchIdAsNoTrackingReturns(Person.BranchId,
        [
            new TimeEntryPolicyBuilder().WithBranchId(Person.BranchId).WithEffectiveFrom(DateTime.MinValue).WithDailyTargetHours(8).WithLunchDeductionOver6H(0).WithLunchDeductionOver4H(0).Build(),
            new TimeEntryPolicyBuilder().WithBranchId(Person.BranchId).WithEffectiveFrom(new DateTime(2026,10,1)).WithDailyTargetHours(6).WithLunchDeductionOver6H(0).WithLunchDeductionOver4H(0).Build()
        ]).Build();
        return new EmployeeHourBankReader(Records, new TimeEntryBalanceProjector(new OperatorsRepositoryBuilder().Build(), Attendance, policies, new TimeEntryCalculationService()));
    }
}
