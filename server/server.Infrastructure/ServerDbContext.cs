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
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Setting> Settings => Set<Setting>();
    public DbSet<Operator> Operators => Set<Operator>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<OperatorAccount> OperatorAccounts => Set<OperatorAccount>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<DailyClose> DailyCloses => Set<DailyClose>();
    public DbSet<DailyCloseItem> DailyCloseItems => Set<DailyCloseItem>();
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<TimeEntrySegment> TimeEntrySegments => Set<TimeEntrySegment>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<IdempotencyRequest> IdempotencyRequests => Set<IdempotencyRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ServerDbContext).Assembly);
    }
}
