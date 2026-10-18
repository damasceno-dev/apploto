using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using server.Communication.Responses;
using server.Exceptions;

namespace server.Configuration;

internal static class SecurityRateLimits
{
    public static void AddSecurityRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy("recovery", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("capability", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "local", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.OnRejected = async (context, ct) =>
            {
                var recovery = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == "recovery";
                context.HttpContext.Response.StatusCode = recovery ? 202 : 429;
                if (!recovery)
                    await context.HttpContext.Response.WriteAsJsonAsync(new ResponseErrorJson(ResourcesErrorMessages.SECURITY_RATE_LIMITED), ct);
            };
        });
    }
}
