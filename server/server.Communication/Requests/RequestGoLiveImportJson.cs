using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestGoLiveImportJson
{
    public string Profile { get; set; } = string.Empty;
    public string Encoding { get; set; } = string.Empty;
    public List<RequestGoLiveSourceFileJson> Files { get; set; } = [];
}

public class RequestGoLiveSourceFileJson
{
    public string Name { get; set; } = string.Empty;
    public string Base64 { get; set; } = string.Empty;
}

public class RequestGoLiveMappingJson
{
    public int ExpectedRevision { get; set; }
    public List<RequestGoLiveResolutionJson> Resolutions { get; set; } = [];
}

public class RequestGoLiveResolutionJson
{
    public string Table { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string? SourceTerminalId { get; set; }
    public string? ExclusionDisposition { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal? TargetValue { get; set; }
    public Guid? AccountId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? OperatorId { get; set; }
    public Guid? TransactionTypeId { get; set; }
    public Guid? PayCategoryId { get; set; }
    public PayItemKind? PayKind { get; set; }
    public string? DeductionPolicy { get; set; }
    public TimeEntryStatus? AttendanceStatus { get; set; }
    public decimal? TargetHours { get; set; }
    public DateTime? PostedOn { get; set; }
    public DateTime? SourceDate { get; set; }
    public string? TargetName { get; set; }
    public SessionCloseField? Field { get; set; }
    public int? Sequence { get; set; }
    public bool HistoryComplete { get; set; }
    public int? Year { get; set; }
    public Guid? TerminalId { get; set; }
    public Guid? ProductId { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
}

public class RequestActivateGoLiveImportJson
{
    public string DryRunVersion { get; set; } = string.Empty;
    public int MappingRevision { get; set; }
    public Guid PreviewId { get; set; }
    public string SourceDigest { get; set; } = string.Empty;
    public string TargetFingerprint { get; set; } = string.Empty;
}
