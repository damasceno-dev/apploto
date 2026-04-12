namespace server.Communication.Requests;

public class RequestUpdateAccountJson
{
    public string Name { get; init; } = string.Empty;
    public string? Institution { get; init; }
    public string? Number { get; init; }
    public Guid? TabAccountId { get; init; }
}
