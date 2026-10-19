using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using server.Domain.Interfaces;
using server.ExceptionHandling;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TokenPlatformOperatorAttribute() : TypeFilterAttribute(typeof(TokenPlatformOperatorFilter));

public sealed class TokenPlatformOperatorFilter(
    IAuthenticationService authentication, IAccountSecurityRepository security, IApiExceptionHandler exceptions)
    : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        try
        {
            var identity = await authentication.GetAuthenticatedUser();
            var current = await security.GetUserForUpdate(identity.Id);
            if (current?.IsPlatformOperator != true)
                throw new TokenWithoutPermissionException(ResourcesErrorMessages.PLATFORM_ONLY);
        }
        catch (Exception error)
        {
            context.Result = exceptions.HandleException(error, context.HttpContext);
        }
    }
}
