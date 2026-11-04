namespace server.Communication.Requests;

/// <summary><c>GET /transaction-type/{id}/reclassification-candidates</c> window (M9.7 Phase 1.3).</summary>
public class RequestReclassificationCandidatesJson
{
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
}
