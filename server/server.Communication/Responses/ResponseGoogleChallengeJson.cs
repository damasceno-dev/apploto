namespace server.Communication.Responses;

public sealed class ResponseGoogleChallengeJson
{
    public Guid ChallengeId { get; init; }
    public string Nonce { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}
