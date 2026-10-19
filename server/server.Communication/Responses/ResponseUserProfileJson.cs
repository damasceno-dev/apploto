using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseUserProfileJson
{
    public Guid UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public bool IsPlatformOperator { get; init; }
    public bool HasPassword { get; init; }
}
