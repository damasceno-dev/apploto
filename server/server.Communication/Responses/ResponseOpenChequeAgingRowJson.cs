using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseOpenChequeAgingRowJson
{
    public Guid TransactionId { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Value { get; init; }

    /// <summary>The part of <see cref="Value"/> still outstanding at the cutoff (M9.7 Phase 1).</summary>
    public decimal OutstandingValue { get; init; }
    public int DaysOutstanding { get; init; }
    public AgingBucket Bucket { get; init; }
}
