using System.Net;

namespace server.Exceptions.Exceptions;

public class ConflictException(string message)
    : ServerException(message)
{
    public override int GetStatusCode => (int)HttpStatusCode.Conflict;

    public override List<string> GetErrorMessages => [Message];
}