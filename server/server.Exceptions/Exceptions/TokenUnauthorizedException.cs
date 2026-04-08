using System.Net;

namespace server.Exceptions.Exceptions;

public class TokenUnauthorizedException(string message)
    : ServerException(message)
{
    public override int GetStatusCode => (int)HttpStatusCode.Unauthorized;

    public override List<string> GetErrorMessages => [Message];
}

public class TokenEmptyException(string errorMessage) : TokenUnauthorizedException(errorMessage);
public class TokenExpiredException(string errorMessage) : TokenUnauthorizedException(errorMessage);
public class TokenInvalidException(string errorMessage) : TokenUnauthorizedException(errorMessage);
public class TokenWithoutUserException(string errorMessage) : TokenUnauthorizedException(errorMessage);