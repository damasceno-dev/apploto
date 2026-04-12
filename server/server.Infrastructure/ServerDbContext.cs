using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;

namespace server.Infrastructure;

public class ServerDbContext(DbContextOptions<ServerDbContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<BranchUser> BranchUsers => Set<BranchUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<TransactionType> TransactionTypes => Set<TransactionType>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Operator> Operators => Set<Operator>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServerDbContext).Assembly);
    }
}
