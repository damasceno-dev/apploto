using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestForgotPasswordJson
{
    public string Email { get; init; } = string.Empty;
}
