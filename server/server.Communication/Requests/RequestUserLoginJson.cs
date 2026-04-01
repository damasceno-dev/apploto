namespace server.Communication.Requests;

public class RequestUserLoginJson
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}