using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class UsersRepository(LotoDbContext dbContext) : IUsersRepository
{
    public async Task Register(User user)
    {
        await dbContext.Users.AddAsync(user);
    }

    public async Task<bool> VerifyIfEmailAlreadyExists(string email)
    {
        return await dbContext.Users.AsNoTracking().AnyAsync(u => u.Email.Trim() == email.Trim());
    }

    public async Task<User?> GetByEmail(string email)
    {
        return await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email.Trim() == email.Trim());
    }

    public async Task<User?> GetById(Guid userId)
    {
        return await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.Active);
    }
}