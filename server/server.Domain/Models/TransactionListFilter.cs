using server.Domain.Entities.Enums;

namespace server.Domain.Models;

public class TransactionListFilter
{
    public Guid? AccountId { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public TransactionStatus? Status { get; set; }
    public Guid? OperatorId { get; set; }
    public Guid? ClientId { get; set; }
    public Guid? OriginTransactionId { get; set; }

    /// <summary>
    /// Server-resolved active-linked-account set for Member callers; never set from a client-bound DTO.
    /// </summary>
    public IReadOnlyList<Guid>? AllowedAccountIds { get; set; }

    public int Page { get; set; }
    public int PageSize { get; set; }
}
