using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseOperatorAccountJson
{
    public Guid Id { get; set; }
    public Guid OperatorId { get; set; }
    public Guid AccountId { get; set; }
    public bool IsPrimary { get; set; }
    public AccountType AccountType { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public string? AccountInstitution { get; set; }
    public string? AccountNumber { get; set; }

    /// <summary>
    /// On a Terminal: the Tab it is paired with (M7.7 Phase 5.6 routing target for the workstation's fiado), else
    /// null. Routing only — it grants nothing: recording fiado still needs that Tab among the operator's own
    /// explicit assignments (decision 1.8).
    /// </summary>
    public Guid? PairedTabAccountId { get; set; }
}
