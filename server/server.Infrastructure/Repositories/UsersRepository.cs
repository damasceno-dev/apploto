using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class UsersRepository(ServerDbContext dbContext) : IUsersRepository
{
    public Task<string> NormalizeEmail(string email) => dbContext.Database
        .SqlQuery<string>($"SELECT lower({email.Trim()}) AS \"Value\"").SingleAsync();

    public async Task Register(User user)
    {
        await dbContext.Users.AddAsync(user);
    }

    public async Task<bool> VerifyIfEmailAlreadyExists(string email)
    {
        var normalizedEmail = await NormalizeEmail(email);
        return await dbContext.Users.AsNoTracking().AnyAsync(u => u.Email == normalizedEmail);
    }

    public async Task<User?> GetByEmail(string email)
    {
        var normalizedEmail = await NormalizeEmail(email);
        return await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalizedEmail);
    }

    public async Task<User?> GetById(Guid userId)
    {
        return await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.Active);
    }
}