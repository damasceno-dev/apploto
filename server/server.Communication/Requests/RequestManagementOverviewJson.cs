namespace server.Communication.Requests;

/// <summary>
/// <c>GET /report/management-overview</c> month selector: both or neither; omitted means the month of branch-local
/// today. A month that has not started is refused. The as-of date is always branch-local today and never a parameter.
/// </summary>
public class RequestManagementOverviewJson
{
    public int? Year { get; init; }
    public int? Month { get; init; }
}
