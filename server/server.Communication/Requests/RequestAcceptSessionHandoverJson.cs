namespace server.Communication.Requests;

/// <summary>The incoming operator's independently observed opening; differing items create a discrepancy.</summary>
public class RequestAcceptSessionHandoverJson
{
    public List<RequestSessionOpeningItemJson>? Items { get; init; }
    public bool ExplicitZero { get; init; }
    public string? Reason { get; init; }
}
