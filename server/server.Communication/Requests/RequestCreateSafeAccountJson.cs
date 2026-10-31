namespace server.Communication.Requests;

/// <summary>A cofre (M9.6 Phase 9): cash custody outside the cashboxes, with no session, count or variance.</summary>
public class RequestCreateSafeAccountJson
{
    public string Name { get; init; } = string.Empty;
    public string? Institution { get; init; }
    public string? Number { get; init; }
}
