using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestOpenChequeAgingJson
{
    public Guid? AccountId { get; init; }
    public Guid? ClientId { get; init; }
    public DateTime? AsOfDate { get; init; }

    /// <summary>
    /// Which event date the <see cref="AsOfDate"/> cutoff compares (M9.7 Phase 1.2): <c>Posted</c> (the default, so a
    /// report as of a locked date is reproducible) or <c>Occurred</c> (operational history).
    /// </summary>
    public ReceivableCutoffBasis? CutoffBasis { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
