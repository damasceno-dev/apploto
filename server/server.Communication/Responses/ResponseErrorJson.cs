namespace server.Communication.Responses;

public class ResponseErrorJson
{
    public List<string> ErrorMessages { get; }

    public ResponseErrorJson(List<string> errors)
    {
        ErrorMessages = errors;
    }
    public ResponseErrorJson(string error)
    {
        ErrorMessages = [error];
    }
}