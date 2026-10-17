namespace server.Domain.Interfaces;

/// <summary>
/// Read-only questions answered by the shared fiado netting projection (§6.4) for previews, which must never be able
/// to write (no transaction repository, no unit of work).
/// </summary>
public interface IFiadoProjectionRepository
{
    /// <summary>
    /// The unpaid remainder a hypothetical new fiado sale would keep after the client's branch-wide FIFO netting as of
    /// <paramref name="asOfDate"/>: the client's In first settles every existing sale ordered before it by
    /// <c>DueDate, Date</c> (existing sales on the same due date and date are treated as older, since the new row's
    /// id is not known yet). Zero means existing credit absorbs the sale and it would not age.
    /// </summary>
    Task<decimal> GetHypotheticalSaleRemainderAsNoTracking(
        Guid branchId,
        Guid clientId,
        DateTime asOfDate,
        DateTime saleDueDate,
        DateTime saleDate,
        decimal saleValue,
        CancellationToken ct = default);
}
