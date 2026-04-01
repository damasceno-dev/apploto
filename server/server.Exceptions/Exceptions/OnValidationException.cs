using System.Net;

namespace server.Exceptions.Exceptions;

public class OnValidationException : LotoException
{
    
    public List<string> ErrorMessages { get; }

    public OnValidationException(List<string> errorMessages) : base(string.Empty)
    {
        ErrorMessages = errorMessages;
    }

    public override int GetStatusCode => (int)HttpStatusCode.BadRequest;

    public override List<string> GetErrorMessages => ErrorMessages;
}