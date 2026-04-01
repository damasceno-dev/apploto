using System.Net;

namespace server.Exceptions.Exceptions;

public class NotFoundException : LotoException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public override int GetStatusCode => (int)HttpStatusCode.NotFound;

    public override List<string> GetErrorMessages => [Message];
}