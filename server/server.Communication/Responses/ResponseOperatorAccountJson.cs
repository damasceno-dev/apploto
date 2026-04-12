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
}
