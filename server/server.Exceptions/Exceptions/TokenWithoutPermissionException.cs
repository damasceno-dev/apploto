using System.Net;

namespace server.Exceptions.Exceptions;

public class TokenWithoutPermissionException(string message)
    : LotoException(message)
{
    public override int GetStatusCode => (int)HttpStatusCode.Forbidden;

    public override List<string> GetErrorMessages => [Message];
}
