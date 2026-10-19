namespace server.Communication.Requests;

public sealed class RequestGoogleSignInJson
{
    public Guid ChallengeId { get; init; }
    public string State { get; init; } = string.Empty;
    public string IdToken { get; init; } = string.Empty;
    public string? InvitationToken { get; init; }
    public string? Name { get; init; }
    public string? CurrentPassword { get; init; }
}
