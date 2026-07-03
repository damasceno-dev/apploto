using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseDashboardNotSubmittedJson
{
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public Guid OperatorId { get; init; }
    public string OperatorName { get; init; } = string.Empty;

    /// <summary>Populated only when a Draft close exists for the account, so the UI can deep-link.</summary>
    public Guid? DailyCloseId { get; init; }
    public DailyCloseStatus? Status { get; init; }
}
