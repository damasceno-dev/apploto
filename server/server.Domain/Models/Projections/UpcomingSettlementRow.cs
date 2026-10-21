using server.Domain.Entities.Enums;

namespace server.Domain.Models.Projections;

/// <summary>One upcoming-settlement row; an installment keeps its plan identity in <paramref name="OriginTransactionId"/>.</summary>
public record UpcomingSettlementRow(
    Guid TransactionId,
    Guid? OriginTransactionId,
    DateTime Date,
    DateTime DueDate,
    decimal Value,
    SettlementInstrument Instrument,
    Guid TransactionTypeId,
    string TransactionTypeName,
    Guid AccountId,
    string AccountName,
    Guid? ClientId,
    string? ClientName);

/// <summary>Whole-filter count and value of one instrument.</summary>
public record UpcomingSettlementInstrumentTotalRow(SettlementInstrument Instrument, int Count, decimal Value);
