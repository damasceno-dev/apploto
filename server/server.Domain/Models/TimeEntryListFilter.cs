using server.Domain.Entities.Enums;

namespace server.Domain.Models;

public class TimeEntryListFilter
{
    public Guid? OperatorId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public TimeEntryStatus? Status { get; set; }
    public bool? IsInProgress { get; set; }
    public bool InProgressFirst { get; set; }
    public bool? Mine { get; set; }

    /// <summary>
    /// Server-resolved active-linked-operator set for Member callers; never set from a client-bound DTO.
    /// </summary>
    public IReadOnlyList<Guid>? AllowedOperatorIds { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
