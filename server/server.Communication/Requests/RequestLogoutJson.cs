using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestLogoutJson
{
    public string RefreshToken { get; init; } = string.Empty;
}
