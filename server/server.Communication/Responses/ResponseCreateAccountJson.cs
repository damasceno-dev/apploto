using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseCreateAccountJson
{
    public Guid Id { get; set; }
    public AccountType Type { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Institution { get; set; }
    public string? Number { get; set; }
    public Guid BranchId { get; set; }
    public Guid? TabAccountId { get; set; }
    public Guid? TerminalAccountId { get; set; }
}
