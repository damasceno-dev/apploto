namespace server.Communication.Responses;

public class ResponseBankStatementAccountBindingJson
{
    public Guid Id { get; init; }
    public uint Version { get; init; }
    public Guid BankAccountId { get; init; }
    public string BankIdentity { get; init; } = string.Empty;
    public string AccountIdentity { get; init; } = string.Empty;
    public string Currency { get; init; } = "BRL";
    public Guid CreatedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
}
