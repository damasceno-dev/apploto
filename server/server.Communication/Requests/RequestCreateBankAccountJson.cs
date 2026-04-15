namespace server.Communication.Requests;

public class RequestCreateBankAccountJson
{
    public string Name { get; init; } = string.Empty;
    public string? Institution { get; init; }
    public string? Number { get; init; }
}
