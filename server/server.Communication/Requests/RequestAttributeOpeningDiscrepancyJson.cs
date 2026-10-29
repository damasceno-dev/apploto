using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestAttributeOpeningDiscrepancyJson
{
    public int DiscrepancyRevision { get; init; }
    public OpeningDiscrepancyAttribution Target { get; init; }
    public string? Reason { get; init; }
}
