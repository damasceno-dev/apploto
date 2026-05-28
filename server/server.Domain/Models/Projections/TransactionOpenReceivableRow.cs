namespace server.Domain.Models.Projections;

public record TransactionOpenReceivableRow(
    Guid Id,
    DateTime Date,
    DateTime DueDate,
    decimal Value,
    Guid? ClientId,
    string? ClientName,
    Guid AccountId,
    string AccountName,
    Guid? OriginTransactionId,
    string? Description);
