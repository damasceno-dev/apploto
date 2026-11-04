namespace server.Domain.Models.Projections;

/// <summary>
/// One open-cheque aging group (M11 Phase 4.2): an installment plan (<paramref name="OriginTransactionId"/> set) or a
/// standalone cheque row (<paramref name="OriginTransactionId"/> null, <paramref name="GroupKey"/> = the row's own id).
/// <paramref name="OpenRows"/> are the group's rows still outstanding at the cutoff, ordered <c>DueDate, Id</c>; the
/// outstanding figures come from the settlement events counted at that cutoff (M9.7 Phase 1).
/// </summary>
public record OpenChequeGroupRow(
    Guid GroupKey,
    Guid? OriginTransactionId,
    decimal OutstandingTotal,
    DateTime OldestOpenDueDate,
    int OpenRowCount,
    int TotalRowCount,
    Guid AccountId,
    string AccountName,
    Guid? ClientId,
    string? ClientName,
    string? Description,
    IReadOnlyList<OpenChequeOpenRow> OpenRows);

public record OpenChequeOpenRow(Guid TransactionId, DateTime DueDate, decimal Value, decimal OutstandingValue);
