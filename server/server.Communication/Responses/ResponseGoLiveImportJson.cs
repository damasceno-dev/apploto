using server.Communication.Requests;

namespace server.Communication.Responses;

public class ResponseGoLiveImportJson
{
    public DateTime? SourceLockDate { get; set; }
    public Guid Id { get; set; }
    public string SourceDigest { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string Profile { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int MappingRevision { get; set; }
    public Guid? PreviewId { get; set; }
    public string? TargetFingerprint { get; set; }
    public List<RequestGoLiveResolutionJson> Suggestions { get; set; } = [];
    public List<RequestGoLiveResolutionJson> Resolutions { get; set; } = [];
    public Dictionary<string, List<Dictionary<string, string>>> Tables { get; set; } = [];
    public ResponseGoLivePreviewJson? Preview { get; set; }
    public List<ResponseGoLiveImportTargetJson> Targets { get; set; } = [];
    public List<Guid> TargetIds { get; set; } = [];
}

public class ResponseGoLivePreviewJson
{
    public string ToolVersion { get; set; } = string.Empty;
    public int QuarantinedRows { get; set; }
    public List<ResponseGoLiveIssueJson> ExplainedIssues { get; set; } = [];
    public Guid Id { get; set; }
    public int MappingRevision { get; set; }
    public string TargetFingerprint { get; set; } = string.Empty;
    public List<ResponseGoLiveIssueJson> Issues { get; set; } = [];
    public int ExcludedRows { get; set; }
    public int SupportedRows { get; set; }
    public List<RequestGoLiveResolutionJson> HistoricalPlan { get; set; } = [];
    public List<RequestGoLiveOpeningJson> OpeningPlan { get; set; } = [];
    public List<ResponseGoLiveAttendanceComparisonJson> AttendanceComparisons {get;set;} = [];
    public List<ResponseGoLiveDayJson> Days { get; set; } = [];
    public bool CanActivate { get; set; }
}

public class ResponseGoLiveIssueJson
{
    public string Table { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? SourceValue { get; set; }
}

public class ResponseGoLiveDayJson
{
    public string SourceAccountId { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? RecordedAccessDifference { get; set; }
    public string? SourceFiadoDelta { get; set; }
    public string? RawAccessDifference { get; set; }
    public string? RawAppDifference { get; set; }
    public string CountedClosing { get; set; } = string.Empty;
    public string? PriorCountedOpening { get; set; }
    public string UnsignedSourceMovements { get; set; } = string.Empty;
    public string SignedSourceMovements { get; set; } = string.Empty;
    public string SourceFeesAndReport { get; set; } = string.Empty;
    public decimal? AccessDifference { get; set; }
    public decimal? AppDifference { get; set; }
    public decimal PositiveRowDifference { get; set; }
    public bool HasPriorCalendarDay { get; set; }
    public string Completeness { get; set; } = "Unresolved";
}

public class ResponseGoLiveImportTargetJson
{
    public string SourceTable { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public Guid TargetId { get; set; }
    public string TargetKind { get; set; } = string.Empty;
}

public class ResponseGoLiveAttendanceComparisonJson
{
    public string SourceId {get;set;} = string.Empty;
    public string SourceTotalHours {get;set;} = string.Empty;
    public string SourceBalanceHours {get;set;} = string.Empty;
    public Guid PolicyId {get;set;}
    public decimal DailyTargetHours {get;set;}
    public decimal LunchDeductionOver6H {get;set;}
    public decimal LunchDeductionOver4H {get;set;}
    public decimal CalculatedTotalHours {get;set;}
    public decimal CalculatedBalanceHours {get;set;}
}
