using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ILoginThrottleRepository
{
    /// <summary>Tracked rows for the given key hashes, row-locked until the surrounding transaction ends.</summary>
    Task<List<LoginThrottle>> GetForUpdate(params string[] keyHashes);
    Task Add(LoginThrottle throttle);
    void Remove(LoginThrottle throttle);
    /// <summary>Stages removal of every partition of one account (its account row and each client row).</summary>
    Task RemoveAccount(string accountKeyHash);
}
