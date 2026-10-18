using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseListOperatorLinkRequestsJson
{
    public IReadOnlyList<ResponseOperatorLinkRequestJson> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
