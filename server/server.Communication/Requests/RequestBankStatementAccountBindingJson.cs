namespace server.Communication.Requests;

public class RequestBankStatementAccountBindingJson
{
    public Guid BankAccountId { get; init; }
    public string BankIdentity { get; init; } = string.Empty;
    public string AccountIdentity { get; init; } = string.Empty;
}

public class RequestListBankStatementAccountBindingsJson
{
    public Guid BankAccountId { get; init; }
}
