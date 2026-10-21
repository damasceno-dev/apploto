using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal sealed class LoginThrottleRepository(ServerDbContext db) : ILoginThrottleRepository
{
    // FOR UPDATE makes the retention sweep (SKIP LOCKED) leave a row alone while a login holds it, and makes a login
    // wait for, then not see, a row the sweep is deleting; a tracked update can therefore never hit a vanished row.
    public Task<List<LoginThrottle>> GetForUpdate(params string[] keyHashes) => db.LoginThrottles
        .FromSql($"SELECT * FROM \"LoginThrottles\" WHERE \"KeyHash\" = ANY({keyHashes}) FOR UPDATE")
        .ToListAsync();

    public async Task Add(LoginThrottle throttle) => await db.LoginThrottles.AddAsync(throttle);

    public void Remove(LoginThrottle throttle) => db.LoginThrottles.Remove(throttle);

    public async Task RemoveAccount(string accountKeyHash) => db.LoginThrottles.RemoveRange(await db.LoginThrottles
        .FromSql($"SELECT * FROM \"LoginThrottles\" WHERE \"AccountKeyHash\" = {accountKeyHash} FOR UPDATE")
        .ToListAsync());
}
