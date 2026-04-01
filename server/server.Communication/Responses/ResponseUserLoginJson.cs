namespace server.Communication.Responses;

public class ResponseUserLoginJson
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ResponseTokenJson ResponseToken { get; set; } = null!;
}