namespace server.Communication.Requests;

public class RequestCreateTerminalJson
{
    public string Name { get; init; } = string.Empty;
    public string? ExternalReference { get; init; }
}
