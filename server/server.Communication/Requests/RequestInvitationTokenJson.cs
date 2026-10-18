using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestInvitationTokenJson
{
    public string Token { get; init; } = string.Empty;
}
