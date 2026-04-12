using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestCreateAccountJson
{
    public AccountType Type { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Institution { get; init; }
    public string? Number { get; init; }
    public Guid? TabAccountId { get; init; }
}
