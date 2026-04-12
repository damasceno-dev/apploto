namespace server.Communication.Requests;

public class RequestUpdateOperatorJson
{
    public string Name { get; init; } = string.Empty;
    public Guid? UserId { get; init; }
}
