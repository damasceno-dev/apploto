namespace server.Communication.Requests;

public class RequestCreateTerminalAccountJson
{
    public string Name { get; init; } = string.Empty;
    public string? Institution { get; init; }
    public string? Number { get; init; }
    public Guid? ExistingTabAccountId { get; init; }
    public bool CreateTabAccount { get; init; }
}
