using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponsePersonalDifferencesJson
{
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public DateTime AsOfDate { get; init; }
    public decimal? CurrentShortageThreshold { get; init; }
    public bool AutomaticPayrollDeduction { get; init; }
    public int UnassignedSourceCount { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public List<ResponsePersonalDifferenceTotalJson> People { get; init; } = [];
    public List<ResponsePersonalDifferenceEntryJson> Entries { get; init; } = [];
}
public class ResponsePersonalDifferenceTotalJson
{
    public Guid? OperatorId { get; init; }
    public string? OperatorName { get; init; }
    public bool? OperatorActive { get; init; }
    public decimal Shortage { get; init; }
    public decimal Overage { get; init; }
    public decimal SignedNet { get; init; }
    public decimal ThresholdShortageBasis { get; init; }
    public int SourceCount { get; init; }
    public bool CurrentThresholdAlert { get; init; }
}
public class ResponsePersonalDifferenceEntryJson
{
    public Guid SourceId { get; init; }
    public PersonalDifferenceSourceKind SourceKind { get; init; }
    public Guid SessionId { get; init; }
    public Guid? OperatorId { get; init; }
    public DateTime SourceDate { get; init; }
    public DateTime PostedOn { get; init; }
    public decimal SourceSignedAmount { get; init; }
    public decimal ShortageContribution { get; init; }
    public decimal OverageContribution { get; init; }
    public decimal ThresholdShortageContribution { get; init; }
    public int? ComparisonRevision { get; init; }
}
