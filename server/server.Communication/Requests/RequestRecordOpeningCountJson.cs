namespace server.Communication.Requests;

public class RequestRecordOpeningCountJson
{
    public Guid AccountId { get; init; }
    public DateTime AsOfDate { get; init; }
    public List<RequestSessionOpeningItemJson>? Items { get; init; }

    /// <summary>The current revision being replaced; null only when the cashbox has no opening count yet.</summary>
    public int? ExpectedRevision { get; init; }
}
