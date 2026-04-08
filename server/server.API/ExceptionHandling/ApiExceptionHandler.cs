using Microsoft.AspNetCore.Mvc;
using server.Communication.Responses;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.ExceptionHandling;

public class ApiExceptionHandler : IApiExceptionHandler
{
    public ObjectResult HandleException(Exception exception, HttpContext httpContext)
    {
        var environment = httpContext.RequestServices.GetService<IWebHostEnvironment>();
        var errorMessage = environment?.EnvironmentName == "Development"
            ? GetErrorDetail(exception, httpContext)
            : ResourcesErrorMessages.UNKNOWN_ERROR;
        return exception is ServerException serverException ?
            new ObjectResult(new ResponseErrorJson(serverException.GetErrorMessages)) {StatusCode = serverException.GetStatusCode} :
            new ObjectResult(new ResponseErrorJson(errorMessage)) { StatusCode = StatusCodes.Status500InternalServerError };
    }
    private static string GetErrorDetail(Exception exception, HttpContext httpContext)
    {
        var innerMessage = exception.InnerException?.Message ?? "No inner exception was thrown";
        var truncatedMessage = innerMessage.Length > 150 ? innerMessage[..150] + "..." : innerMessage;
        return $"Método: {httpContext.Request.Method} {httpContext.Request.Path}, Erro: {exception.Message}, Exception: {truncatedMessage}";
    }
}