using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Operator = server.Domain.Entities.Operator;

namespace CommonTestUtilities.Entities;

public class TimeEntryBuilder
{
    private Guid _id = Guid.NewGuid();
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _active = true;
    private DateTime _date = DateTime.Today;
    private TimeOnly? _clockIn = new TimeOnly(8, 0);
    private TimeOnly? _clockOut = new TimeOnly(17, 0);
    private TimeEntryStatus _status = TimeEntryStatus.Present;
    private decimal _totalHours = 8m;
    private decimal _balanceHours = 0.67m;
    private Guid _operatorId = Guid.NewGuid();
    private Guid _branchId = Guid.NewGuid();
    private DateTime? _updatedAt;
    private Guid? _updatedByUserId;
    private Operator? _operator;

    public TimeEntryBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TimeEntryBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public TimeEntryBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public TimeEntryBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public TimeEntryBuilder WithClockIn(TimeOnly? clockIn)
    {
        _clockIn = clockIn;
        return this;
    }

    public TimeEntryBuilder WithClockOut(TimeOnly? clockOut)
    {
        _clockOut = clockOut;
        return this;
    }

    public TimeEntryBuilder WithStatus(TimeEntryStatus status)
    {
        _status = status;
        return this;
    }

    public TimeEntryBuilder WithTotalHours(decimal totalHours)
    {
        _totalHours = totalHours;
        return this;
    }

    public TimeEntryBuilder WithBalanceHours(decimal balanceHours)
    {
        _balanceHours = balanceHours;
        return this;
    }

    public TimeEntryBuilder WithOperatorId(Guid operatorId)
    {
        _operatorId = operatorId;
        return this;
    }

    public TimeEntryBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        return this;
    }

    public TimeEntryBuilder WithUpdated(DateTime updatedAt, Guid updatedByUserId)
    {
        _updatedAt = updatedAt;
        _updatedByUserId = updatedByUserId;
        return this;
    }

    public TimeEntryBuilder WithOperator(Operator op)
    {
        _operator = op;
        _operatorId = op.Id;
        _branchId = op.BranchId;
        return this;
    }

    public TimeEntry Build()
    {
        var op = _operator ?? new OperatorBuilder().WithId(_operatorId).WithBranchId(_branchId).Build();

        return new TimeEntry
        {
            Id = _id,
            CreatedAt = _createdAt,
            Active = _active,
            Date = _date,
            ClockIn = _clockIn,
            ClockOut = _clockOut,
            Status = _status,
            TotalHours = _totalHours,
            BalanceHours = _balanceHours,
            OperatorId = _operatorId,
            Operator = op,
            BranchId = _branchId,
            UpdatedAt = _updatedAt,
            UpdatedByUserId = _updatedByUserId
        };
    }
}
