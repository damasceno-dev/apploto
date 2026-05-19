namespace server.Communication.Requests;

public class RequestCreateProductJson
{
    public string Name { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
}
