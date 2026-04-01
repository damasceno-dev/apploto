using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.ExceptionHandling;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class MyTokenAuthenticateFilter() : TypeFilterAttribute(typeof(TokenAuthenticateFilter));


public class TokenAuthenticateFilter(IAuthenticationService authenticationService, IApiExceptionHandler exceptionHandler) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            await authenticationService.GetAuthenticatedUser();
        }
        catch (Exception exception)
        {
            context.Result = exceptionHandler.HandleException(exception, context.HttpContext);
        }
    }
}