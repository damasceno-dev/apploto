using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestChangePasswordJson
{
    public string CurrentPassword { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
