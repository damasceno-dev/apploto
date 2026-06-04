using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseReceivableImpactJson
{
    public AgingBucket? BucketBefore { get; init; }
    public AgingBucket? BucketAfter { get; init; }
    public bool RowAppearsInOpenReceivables { get; init; }
    public bool RowDisappearsFromOpenReceivables { get; init; }
}
