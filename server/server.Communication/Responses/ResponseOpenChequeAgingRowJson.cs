using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseOpenChequeAgingRowJson
{
    public Guid TransactionId { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Value { get; init; }
    public int DaysOutstanding { get; init; }
    public AgingBucket Bucket { get; init; }
}
