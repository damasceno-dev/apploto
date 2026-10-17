namespace server.Domain.Models.Projections;

/// <summary>
/// One fiado sale (Tab <c>Out</c>) that still holds a positive unpaid remainder after its client's branch-wide
/// FIFO netting (§6.4). <see cref="Value"/> is that remainder, never the original sale value.
/// </summary>
public record FiadoOpenSaleRow(
    Guid Id,
    DateTime Date,
    DateTime DueDate,
    decimal Value,
    Guid ClientId,
    string ClientName,
    Guid AccountId,
    string AccountName,
    string? Description);
