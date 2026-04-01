using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.ExceptionHandling;

namespace server.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class MyTokenAuthorizeFilter : TypeFilterAttribute
{
    public MyTokenAuthorizeFilter(Role requiredRole, params Role[] additionalRoles) : base(typeof(TokenAuthorizeFilter))
    {
        Arguments = [requiredRole, additionalRoles];
    }
}


public class TokenAuthorizeFilter(IApiExceptionHandler exceptionHandler, IAuthenticationService authenticationService, Role requiredRole, params Role[] additionalRoles) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            await authenticationService.GetAuthorizedUser(requiredRole, additionalRoles);
        }
        catch (Exception exception)
        {
            context.Result = exceptionHandler.HandleException(exception, context.HttpContext);
        }
    }
    
}