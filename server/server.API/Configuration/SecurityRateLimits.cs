using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using server.Application.Services.Security;
using server.Communication.Responses;
using server.Exceptions;
using server.Headers;
using server.Network;

namespace server.Configuration;

/// <summary>
/// Process-local per-client-network request limits for anonymous security endpoints. Each runs before binding,
/// validation, the database and BCrypt; the persistent login budgets live in <see cref="LoginAttemptGuard"/>.
/// </summary>
internal static class SecurityRateLimits
{
    public const string Login = "login";
    public const string Recovery = "recovery";
    public const string Capability = "capability";

    private const int RecoveryRequestsPerMinute = 60;
    private const int CapabilityRequestsPerMinute = 120;

    public static void AddSecurityRateLimits(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(Recovery, context => PerClientNetwork(context, RecoveryRequestsPerMinute));
            options.AddPolicy(Capability, context => PerClientNetwork(context, CapabilityRequestsPerMinute));
            options.AddPolicy(Login, context => PerClientNetwork(context,
                context.RequestServices.GetRequiredService<LoginProtectionOptions>().RequestsPerMinute));
            options.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                // Forgot-password stays an empty 202 with no retry header: its answer never depends on the target.
                if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == Recovery)
                {
                    response.StatusCode = StatusCodes.Status202Accepted;
                    return;
                }

                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    RetryAfterHeader.Write(response, retryAfter);
                await response.WriteAsJsonAsync(new ResponseErrorJson(ResourcesErrorMessages.SECURITY_RATE_LIMITED), ct);
            };
        });
    }

    private static RateLimitPartition<string> PerClientNetwork(HttpContext context, int permitsPerMinute)
    {
        var time = context.RequestServices.GetRequiredService<TimeProvider>();
        return RateLimitPartition.Get(ClientNetwork.Of(context.Connection.RemoteIpAddress),
            _ => new ClientWindowRateLimiter(time, permitsPerMinute, TimeSpan.FromMinutes(1)));
    }
}
