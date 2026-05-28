namespace server.Domain.Models.Projections;

public record OpenChequeGroupRow(
    Guid OriginTransactionId,
    decimal OutstandingTotal,
    DateTime OldestOpenDueDate,
    int OpenRowCount,
    int TotalRowCount,
    Guid AccountId,
    string AccountName,
    Guid? ClientId,
    string? ClientName,
    string? Description);
