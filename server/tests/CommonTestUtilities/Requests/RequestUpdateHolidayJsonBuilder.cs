using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUpdateHolidayJsonBuilder
{
    private string? _description = "Updated description";

    public RequestUpdateHolidayJsonBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public RequestUpdateHolidayJson Build() => new() { Description = _description };
}
