using System.Net;

namespace server.Exceptions.Exceptions;

public class OnValidationException(List<string> errorMessages)
    : ServerException(string.Empty)
{
    private List<string> ErrorMessages { get; } = errorMessages;

    public override int GetStatusCode => (int)HttpStatusCode.BadRequest;

    public override List<string> GetErrorMessages => ErrorMessages;
}