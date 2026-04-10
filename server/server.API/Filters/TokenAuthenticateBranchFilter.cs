using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using server.Domain.Interfaces;
using server.ExceptionHandling;

namespace server.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class TokenAuthenticateBranchAttribute() : TypeFilterAttribute(typeof(TokenAuthenticateBranchFilter));


public class TokenAuthenticateBranchFilter(IAuthenticationService authenticationService, IApiExceptionHandler exceptionHandler) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            await authenticationService.GetAuthenticatedBranchUser();
        }
        catch (Exception exception)
        {
            context.Result = exceptionHandler.HandleException(exception, context.HttpContext);
        }
    }
}