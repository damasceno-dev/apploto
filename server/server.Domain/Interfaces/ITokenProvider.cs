using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ITokenProvider
{
    string? GetTokenValue();
    User? GetCachedUser();
    void CacheUser(User user);
}