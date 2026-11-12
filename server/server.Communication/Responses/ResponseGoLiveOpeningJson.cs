using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseGoLiveOpeningJson
{
    public Guid RecordedByOperatorId { get; set; }
    public bool ClientCreditObserved { get; set; }
    public List<ResponseGoLiveOpeningItemJson> Items { get; set; } = [];
    public uint Version { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ClientId { get; set; }
    public bool IsCheque { get; set; }
    public DateTime AsOfDate { get; set; }
    public string SourceReference { get; set; } = string.Empty;
    public string SourceEvidence { get; set; } = string.Empty;
    public Guid? SourceImportId { get; set; }
    public Guid? PreviousId { get; set; }
    public Guid Id { get; set; }
    public bool Active { get; set; }
    public List<Guid> TransactionIds { get; set; } = [];
}

public class ResponseGoLiveOpeningItemJson
{
    public Guid Id { get; set; }
    public decimal Value { get; set; }
    public DateTime DueDate { get; set; }
    public TransactionStatus Status { get; set; }
}
