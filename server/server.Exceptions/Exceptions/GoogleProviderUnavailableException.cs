using System.Net;

namespace server.Exceptions.Exceptions;

public sealed class GoogleProviderUnavailableException(string message) : ServerException(message)
{
    public override int GetStatusCode => (int)HttpStatusCode.ServiceUnavailable;
    public override List<string> GetErrorMessages => [Message];
}
