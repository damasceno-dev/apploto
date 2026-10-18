using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestResetPasswordJson
{
    public string Token { get; init; } = string.Empty;
    public string NewPassword { get; init; } = string.Empty;
}
