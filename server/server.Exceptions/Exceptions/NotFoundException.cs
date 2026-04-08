using System.Net;

namespace server.Exceptions.Exceptions;

public class NotFoundException(string message)
    : ServerException(message)
{
    public override int GetStatusCode => (int)HttpStatusCode.NotFound;

    public override List<string> GetErrorMessages => [Message];
}