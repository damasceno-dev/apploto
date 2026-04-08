namespace server.Exceptions.Exceptions;

public abstract class ServerException(string message)
    : SystemException(message)
{
    public abstract int GetStatusCode { get; }
    public abstract List<string> GetErrorMessages { get; }
}
