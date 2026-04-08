using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;

namespace server.Infrastructure;

internal class ServerDbContext(DbContextOptions options)
    : DbContext(options)
{
    public DbSet<User> Users { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
}
