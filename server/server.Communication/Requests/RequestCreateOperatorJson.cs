namespace server.Communication.Requests;

public class RequestCreateOperatorJson
{
    public string Name { get; init; } = string.Empty;
    public Guid? UserId { get; init; }
}
