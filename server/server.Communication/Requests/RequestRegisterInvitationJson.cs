using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestRegisterInvitationJson
{
    public string Token { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
}
