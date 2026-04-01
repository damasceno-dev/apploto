using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using server.Communication.Responses;
using server.ExceptionHandling;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Filters;

public class ExceptionFilter(IApiExceptionHandler exceptionHandler) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        context.Result = exceptionHandler.HandleException(context.Exception, context.HttpContext);
    }
}