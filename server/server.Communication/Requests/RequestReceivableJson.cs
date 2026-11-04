using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

/// <summary><c>GET /receivable/{transactionId}</c> cutoff (M9.7 Phase 1.2).</summary>
public class RequestReceivableJson
{
    /// <summary>Cutoff date; omitted means branch-local today.</summary>
    public DateTime? CutoffDate { get; init; }

    /// <summary>Omitted means <c>Posted</c>.</summary>
    public ReceivableCutoffBasis? CutoffBasis { get; init; }
}
