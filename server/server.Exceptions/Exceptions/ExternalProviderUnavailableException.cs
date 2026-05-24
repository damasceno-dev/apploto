using System.Net;

namespace server.Exceptions.Exceptions;

public class ExternalProviderUnavailableException(string message)
    : ServerException(message)
{
    public override int GetStatusCode => (int)HttpStatusCode.BadGateway;

    public override List<string> GetErrorMessages => [Message];
}
