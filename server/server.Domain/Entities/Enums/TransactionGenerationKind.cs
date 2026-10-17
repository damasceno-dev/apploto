namespace server.Domain.Entities.Enums;

/// <summary>
/// Why the system generated a transaction (M7.7 Phase 5.6/5.7, §3.12). A generated row is the Terminal leg of a
/// fiado operation that moved counted stock or cash; it never exists without its Tab source.
/// </summary>
public enum TransactionGenerationKind
{
    /// <summary>Terminal <c>Out</c>: counted stock handed out on a fiado sale.</summary>
    FiadoCountedStockSale = 1,

    /// <summary>Terminal <c>In</c>: cash received into the paired terminal's counted drawer on a fiado repayment.</summary>
    FiadoCashRepayment = 2
}
