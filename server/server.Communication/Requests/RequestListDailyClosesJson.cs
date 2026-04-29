using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestListDailyClosesJson
{
    public Guid? AccountId { get; init; }
    public DailyCloseStatus? Status { get; init; }
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public Guid? OperatorId { get; init; }

    /// <summary>
    /// Convenience filter that resolves to the caller's linked operator id server-side.
    /// Cannot be combined with an explicit <see cref="OperatorId"/>.
    /// </summary>
    public bool Mine { get; init; }

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
