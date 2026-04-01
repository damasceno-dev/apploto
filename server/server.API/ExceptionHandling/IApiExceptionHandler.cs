using Microsoft.AspNetCore.Mvc;

namespace server.ExceptionHandling;

public interface IApiExceptionHandler
{
    ObjectResult HandleException(Exception exception, HttpContext httpContext);
}