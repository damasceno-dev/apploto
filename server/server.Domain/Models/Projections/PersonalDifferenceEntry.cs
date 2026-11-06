using server.Domain.Entities.Enums;

namespace server.Domain.Models.Projections;

public class PersonalDifferenceEntry
{
    public Guid SourceId { get; init; }
    public PersonalDifferenceSourceKind SourceKind { get; init; }
    public Guid SessionId { get; init; }
    public Guid? OperatorId { get; init; }
    public DateTime SourceDate { get; init; }
    public DateTime PostedOn { get; init; }
    public decimal SourceSignedAmount { get; init; }
    public decimal Shortage { get; init; }
    public decimal Overage { get; init; }
    public decimal ThresholdShortageContribution { get; init; }
    public int? ComparisonRevision { get; init; }
}
public record PersonalDifferenceTotal(
    Guid? OperatorId,
    string? OperatorName,
    bool? OperatorActive,
    decimal Shortage,
    decimal Overage,
    int Count,
    decimal ThresholdShortageBasis);
public record PersonalDifferencePage(IReadOnlyList<PersonalDifferenceTotal> Totals, int TotalCount, IReadOnlyList<PersonalDifferenceEntry> Entries);
