using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestCreateCategoryJson
{
    public string Name { get; init; } = string.Empty;
    public Direction DefaultDirection { get; init; }
}
