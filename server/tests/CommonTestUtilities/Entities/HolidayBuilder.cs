using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class HolidayBuilder
{
    private Guid _id = Guid.NewGuid();
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _active = true;
    private DateTime _date = DateTime.Today;
    private string? _description = "Feriado";
    private Guid _branchId = Guid.NewGuid();

    public HolidayBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public HolidayBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public HolidayBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public HolidayBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public HolidayBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public HolidayBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        return this;
    }

    public Holiday Build()
    {
        return new Holiday
        {
            Id = _id,
            CreatedAt = _createdAt,
            Active = _active,
            Date = _date,
            Description = _description,
            BranchId = _branchId
        };
    }
}
