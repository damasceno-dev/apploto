using server.Domain.Entities.Enums;

namespace server.Domain.Models.Projections;

/// <summary>A client's fiado movement with its full-history running balance (§6.4 sign convention).</summary>
public record FiadoStatementRow(
    Guid Id,
    DateTime Date,
    Direction Direction,
    decimal Value,
    Guid AccountId,
    string AccountName,
    string TransactionTypeName,
    string? Description,
    decimal RunningBalance);
