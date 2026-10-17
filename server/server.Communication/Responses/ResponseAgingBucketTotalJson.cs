using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>One aging bucket's whole-set total (M11 Phases 2.1 and 3.2): the open value and how many rows hold it.</summary>
public class ResponseAgingBucketTotalJson
{
    public AgingBucket Bucket { get; init; }
    public decimal Value { get; init; }
    public int RowCount { get; init; }
}
