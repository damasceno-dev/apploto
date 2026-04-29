using server.Domain.Entities.Enums;

namespace server.Domain.Models;

public class DailyCloseListFilter
{
    public Guid? AccountId { get; set; }
    public DailyCloseStatus? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public Guid? OperatorId { get; set; }
    public bool? Mine { get; set; }

    /// <summary>
    /// Server-resolved active-linked-account set for Member callers; never set from a client-bound DTO.
    /// </summary>
    public IReadOnlyList<Guid>? AllowedAccountIds { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
