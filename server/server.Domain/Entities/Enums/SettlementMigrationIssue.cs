namespace server.Domain.Entities.Enums;

/// <summary>
/// What the upgrade could not establish about a legacy <c>PaidAt</c> (M9.7 Phase 1.4). The flags are provenance,
/// never a correction: the original instant is kept and no date is invented.
/// </summary>
[Flags]
public enum SettlementMigrationIssue
{
    None = 0,

    /// <summary>A Tab row: <c>PaidAt</c> never settled fiado (FIFO netting), so the event changes no outstanding amount.</summary>
    NotSettleableTabRow = 1,

    /// <summary>The <c>PaidAt</c> branch-local date is earlier than the row's business date.</summary>
    OccurredBeforeTransactionDate = 2,

    /// <summary>The row was still a Draft when the upgrade ran.</summary>
    DraftAtMigration = 4,

    /// <summary>The row was already cancelled when the upgrade ran.</summary>
    CancelledAtMigration = 8
}
