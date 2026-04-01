using System.Net;

namespace server.Exceptions.Exceptions;

public class ConflictException : LotoException
{
    public ConflictException(string message) : base(message)
    {
    }

    public override int GetStatusCode => (int)HttpStatusCode.Conflict;

    public override List<string> GetErrorMessages => [Message];
}