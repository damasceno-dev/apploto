namespace server.Communication.Requests;

public class RequestGoLiveOpeningJson
{
    public Guid AccountId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid TransactionTypeId { get; set; }
    public Guid RecordedByOperatorId { get; set; }
    public DateTime AsOfDate { get; set; }
    public bool IsCheque { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string SourceEvidence { get; set; } = string.Empty;
    public List<RequestGoLiveOpeningItemJson> Items { get; set; } = [];
}

public class RequestGoLiveOpeningItemJson
{
    public decimal Value { get; set; }
    public DateTime DueDate { get; set; }
}

public class RequestCancelGoLiveOpeningJson
{
    public string Reason { get; set; } = string.Empty;
}

public class RequestAmendGoLiveOpeningJson
{
    public string Reason { get; set; } = string.Empty;
    public RequestGoLiveOpeningJson Opening { get; set; } = new();
}
