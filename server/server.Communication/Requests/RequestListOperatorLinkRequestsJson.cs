using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestListOperatorLinkRequestsJson
{
    public OperatorLinkRequestStatus? Status { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
