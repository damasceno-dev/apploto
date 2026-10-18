using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestUpdateProfileJson
{
    public string Name { get; init; } = string.Empty;
}
