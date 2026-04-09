using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Token;

public class HttpContextTokenProvider(IHttpContextAccessor contextAccessor) : ITokenProvider
{
    private const string CachedUserKey = "AuthenticatedUser";
    private const string CachedBranchUserKey = "AuthenticatedBranchUser";

    public string? GetTokenValue()
    {
        var context = contextAccessor.HttpContext ?? throw new ArgumentException("contextAccessor.HttpContext cannot be null");

        var authHeader = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authHeader) || authHeader.StartsWith("Bearer ") is false)
            return null;
        
        return authHeader["Bearer ".Length..];
    }

    public User? GetCachedUser()
    {
        var context = contextAccessor.HttpContext ?? throw new ArgumentException("contextAccessor.HttpContext cannot be null");
        return context.Items[CachedUserKey] as User;
    }

    public void CacheUser(User user)
    {
        var context = contextAccessor.HttpContext ?? throw new ArgumentException("contextAccessor.HttpContext cannot be null");
        context.Items[CachedUserKey] = user;
    }

    public BranchUser? GetCachedBranchUser()
    {
        var context = contextAccessor.HttpContext ?? throw new ArgumentException("contextAccessor.HttpContext cannot be null");
        return context.Items[CachedBranchUserKey] as BranchUser;
    }

    public void CacheBranchUser(BranchUser branchUser)
    {
        var context = contextAccessor.HttpContext ?? throw new ArgumentException("contextAccessor.HttpContext cannot be null");
        context.Items[CachedBranchUserKey] = branchUser;
    }
}
