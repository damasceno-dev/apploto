namespace server.Communication.Requests;

/// <summary>
/// Query for <c>GET /client/{clientId}/fiado-statement</c>. The window is optional: without <see cref="DateFrom"/>
/// the statement starts at the client's first fiado movement, and without <see cref="DateTo"/> it ends at the
/// branch-local business date.
/// </summary>
public class RequestClientFiadoStatementJson
{
    public DateTime? DateFrom { get; init; }
    public DateTime? DateTo { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
