using System.Net;

namespace server.Exceptions.Exceptions;

public class InvalidLoginException(string message)
    : LotoException(message)
{
    public override int GetStatusCode => (int)HttpStatusCode.Unauthorized;

    public override List<string> GetErrorMessages => [Message];
}