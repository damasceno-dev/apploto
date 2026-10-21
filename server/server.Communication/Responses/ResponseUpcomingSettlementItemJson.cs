using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>One unpaid classified row; an installment keeps its plan in <see cref="OriginTransactionId"/>.</summary>
public class ResponseUpcomingSettlementItemJson
{
    public Guid TransactionId { get; init; }
    public Guid? OriginTransactionId { get; init; }
    public DateTime Date { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Value { get; init; }

    /// <summary>The row's type's current classification.</summary>
    public SettlementInstrument Instrument { get; init; }
    public Guid TransactionTypeId { get; init; }
    public string TransactionTypeName { get; init; } = string.Empty;
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public Guid? ClientId { get; init; }
    public string? ClientName { get; init; }
}
