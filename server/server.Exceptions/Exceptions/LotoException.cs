namespace server.Exceptions.Exceptions;

public abstract class LotoException : SystemException
{
    public LotoException(string message) : base(message)
    {
    }
    public abstract int GetStatusCode { get; }
    public abstract List<string> GetErrorMessages { get; }
}