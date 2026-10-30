namespace server.Communication.Responses;

public class ResponseTerminalJson
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
    public bool Active { get; init; }
}
