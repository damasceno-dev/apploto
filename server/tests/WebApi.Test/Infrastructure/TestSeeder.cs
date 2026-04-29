using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Infrastructure;
using Operator = server.Domain.Entities.Operator;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// Seeds deterministic test data directly into the container-backed database and
/// issues matching tokens via the real <see cref="ITokenServices"/>. Every call uses
/// unique identifiers so tests can run side-by-side inside the same database without
/// colliding on unique constraints (email, <c>(UserId, BranchId)</c>, etc.).
/// </summary>
internal static class TestSeeder
{
    public const string DefaultPassword = "Password123";

    extension(ServerWebApplicationFactory factory)
    {
        public async Task<(User User, Branch Branch, BranchUser Membership, string Token)> SeedFullBranchContextAsync(
            string label,
            Role role = Role.Admin)
        {
            var user = await factory.SeedUserAsync();
            var branch = await factory.SeedBranchAsync($"{label} {Guid.NewGuid():N}");
            var membership = await factory.SeedBranchUserAsync(user.Id, branch.Id, role);
            var token = factory.IssueBranchToken(membership);

            return (user, branch, membership, token);
        }

        public async Task<User> SeedUserAsync(string? email = null,
            string? name = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var passwordEncryption = scope.ServiceProvider.GetRequiredService<PasswordEncryption>();

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Test User {Guid.NewGuid():N}",
                Email = email ?? $"user-{Guid.NewGuid():N}@example.com",
                Password = passwordEncryption.HashPassword(DefaultPassword)
            };

            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            return user;
        }

        private async Task<Branch> SeedBranchAsync(string? name = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var branch = new Branch
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Branch {Guid.NewGuid():N}"
            };
            dbContext.Branches.Add(branch);
            await dbContext.SaveChangesAsync();
            return branch;
        }

        /// <summary>
        /// Seeds a standalone branch for cross-branch isolation tests where the caller only
        /// needs a different <see cref="Branch"/> id (no user/membership context).
        /// </summary>
        public Task<Branch> SeedBranchForOtherContextAsync(string? name = null)
        {
            return factory.SeedBranchAsync(name ?? $"Other Branch {Guid.NewGuid():N}");
        }

        public async Task<BranchUser> SeedBranchUserAsync(Guid userId,
            Guid branchId,
            Role role)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var branchUser = new BranchUser
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                BranchId = branchId,
                Role = role
            };
            dbContext.BranchUsers.Add(branchUser);
            await dbContext.SaveChangesAsync();
            return branchUser;
        }

        /// <summary>
        /// Issues a real global JWT for the given user using the production
        /// <see cref="ITokenServices"/> wired into the factory.
        /// </summary>
        public string IssueGlobalToken(User user)
        {
            using var scope = factory.Services.CreateScope();
            var tokenServices = scope.ServiceProvider.GetRequiredService<ITokenServices>();
            return tokenServices.GenerateGlobalToken(user);
        }

        /// <summary>
        /// Issues a real branch-scoped JWT for the given membership using the production
        /// <see cref="ITokenServices"/> wired into the factory.
        /// </summary>
        public string IssueBranchToken(BranchUser branchUser)
        {
            using var scope = factory.Services.CreateScope();
            var tokenServices = scope.ServiceProvider.GetRequiredService<ITokenServices>();
            return tokenServices.GenerateBranchToken(branchUser);
        }

        public async Task<RefreshToken> SeedRefreshTokenAsync(Guid userId)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var refreshToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Value = Convert.ToBase64String(Guid.NewGuid().ToByteArray()),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.RefreshTokens.Add(refreshToken);
            await dbContext.SaveChangesAsync();
            return refreshToken;
        }

        public async Task<Client> SeedClientAsync(
            Guid branchId,
            string? name = null,
            string? phone = null,
            string? cpf = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var client = new Client
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Client {Guid.NewGuid():N}",
                Phone = phone ?? "11999999999",
                Cpf = cpf,
                BranchId = branchId
            };
            dbContext.Clients.Add(client);
            await dbContext.SaveChangesAsync();
            return client;
        }

        public async Task<Operator> SeedOperatorAsync(Guid branchId, string? name = null, Guid? userId = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            if (userId is { } linkedUserId)
            {
                var existingOperatorId = await dbContext.Operators
                    .AsNoTracking()
                    .Where(op =>
                        op.BranchId == branchId &&
                        op.UserId == linkedUserId &&
                        op.Active)
                    .Select(op => (Guid?)op.Id)
                    .FirstOrDefaultAsync();

                if (existingOperatorId.HasValue)
                {
                    throw new InvalidOperationException(
                        $"SeedOperatorAsync fixture setup violated the active operator user-link invariant. " +
                        $"BranchId '{branchId}' and UserId '{linkedUserId}' are already linked to active OperatorId '{existingOperatorId}'. " +
                        "Deactivate the existing seeded operator, seed the new operator without a UserId, or drive the duplicate-link scenario through the API/use case.");
                }
            }

            var op = new Operator
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Operator {Guid.NewGuid():N}",
                BranchId = branchId,
                UserId = userId
            };
            dbContext.Operators.Add(op);
            await dbContext.SaveChangesAsync();
            return op;
        }

        public async Task<Account> SeedAccountAsync(
            Guid branchId,
            AccountType type = AccountType.Terminal,
            string? name = null,
            string? institution = null,
            string? number = null,
            Guid? tabAccountId = null,
            bool active = true)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var account = new Account
            {
                Id = Guid.NewGuid(),
                Type = type,
                Name = name ?? $"Account {Guid.NewGuid():N}",
                Institution = institution,
                Number = number,
                BranchId = branchId,
                TabAccountId = tabAccountId,
                Active = active
            };

            dbContext.Accounts.Add(account);
            await dbContext.SaveChangesAsync();
            return account;
        }

        public async Task<OperatorAccount> SeedOperatorAccountAsync(
            Guid operatorId,
            Guid accountId,
            bool isPrimary = false,
            bool active = true)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var operatorAccount = new OperatorAccount
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                AccountId = accountId,
                IsPrimary = isPrimary,
                Active = active
            };

            dbContext.OperatorAccounts.Add(operatorAccount);
            await dbContext.SaveChangesAsync();
            return operatorAccount;
        }

        public async Task<List<OperatorAccount>> ListOperatorAccountsByOperatorIdAsync(Guid operatorId)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            return await dbContext.OperatorAccounts
                .AsNoTracking()
                .Where(operatorAccount => operatorAccount.OperatorId == operatorId)
                .OrderBy(operatorAccount => operatorAccount.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<OperatorAccount>> ListOperatorAccountsByAccountIdAsync(Guid accountId)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            return await dbContext.OperatorAccounts
                .AsNoTracking()
                .Where(operatorAccount => operatorAccount.AccountId == accountId)
                .OrderBy(operatorAccount => operatorAccount.CreatedAt)
                .ToListAsync();
        }

        public async Task<Category> SeedCategoryAsync(
            Guid branchId,
            string? name = null,
            Direction defaultDirection = Direction.In)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"Category {Guid.NewGuid():N}",
                DefaultDirection = defaultDirection,
                BranchId = branchId
            };
            dbContext.Categories.Add(category);
            await dbContext.SaveChangesAsync();
            return category;
        }

        public async Task<TransactionType> SeedTransactionTypeAsync(
            Guid categoryId,
            string? name = null,
            SettlementRule settlementRule = SettlementRule.SameDay,
            bool requiresTabAccountAndClient = false,
            bool active = true)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var transactionType = new TransactionType
            {
                Id = Guid.NewGuid(),
                Name = name ?? $"TType {Guid.NewGuid():N}",
                CategoryId = categoryId,
                SettlementRule = settlementRule,
                RequiresTabAccountAndClient = requiresTabAccountAndClient,
                Active = active
            };
            dbContext.TransactionTypes.Add(transactionType);
            await dbContext.SaveChangesAsync();
            return transactionType;
        }

        public async Task<Transaction> SeedTransactionAsync(
            Guid branchId,
            Guid accountId,
            Guid transactionTypeId,
            Guid categoryId,
            Direction direction,
            Guid recordedByOperatorId,
            Guid createdByUserId,
            DateTime? date = null,
            decimal value = 10m,
            string? description = null,
            TimeOnly? transactionTime = null,
            Guid? clientId = null,
            DateTime? dueDate = null,
            DateTime? paidAt = null,
            Guid? originTransactionId = null,
            TransactionStatus status = TransactionStatus.Active,
            DateTime? createdAt = null,
            Guid? id = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            var transactionDate = date ?? DateTime.Today;

            var transaction = new Transaction
            {
                Id = id ?? Guid.NewGuid(),
                Date = transactionDate,
                Value = value,
                Description = description ?? $"Transaction {Guid.NewGuid():N}",
                TransactionTime = transactionTime,
                TransactionTypeId = transactionTypeId,
                CategoryId = categoryId,
                Direction = direction,
                AccountId = accountId,
                ClientId = clientId,
                DueDate = dueDate ?? transactionDate,
                PaidAt = paidAt is null ? null : AsUtc(paidAt.Value),
                OriginTransactionId = originTransactionId,
                RecordedByOperatorId = recordedByOperatorId,
                CreatedByUserId = createdByUserId,
                Status = status,
                BranchId = branchId,
                CreatedAt = AsUtc(createdAt ?? DateTime.UtcNow)
            };

            dbContext.Transactions.Add(transaction);
            await dbContext.SaveChangesAsync();
            return transaction;
        }

        public async Task<Setting> SeedSettingAsync(Guid branchId, DateTime? lockDate = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var setting = new Setting
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                LockDate = lockDate ?? DateTime.MinValue
            };
            dbContext.Settings.Add(setting);
            await dbContext.SaveChangesAsync();
            return setting;
        }

        public async Task<DailyClose> SeedDailyCloseAsync(
            Guid branchId,
            Guid accountId,
            DateTime? date = null,
            DailyCloseStatus status = DailyCloseStatus.Draft,
            Guid? submittedByOperatorId = null,
            DateTime? submittedAt = null)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            var dailyClose = new DailyClose
            {
                Id = Guid.NewGuid(),
                BranchId = branchId,
                AccountId = accountId,
                Date = date ?? DateTime.Today,
                Status = status,
                SubmittedByOperatorId = submittedByOperatorId,
                SubmittedAt = submittedAt is null ? null : AsUtc(submittedAt.Value)
            };

            dbContext.DailyCloses.Add(dailyClose);
            await dbContext.SaveChangesAsync();
            return dailyClose;
        }

        /// <summary>
        /// Reloads an entity from the database so tests can assert persisted state without
        /// relying on cached tracking from the seed calls.
        /// </summary>
        public async Task<TEntity?> ReloadAsync<TEntity>(Guid entityId)
            where TEntity : EntityBase
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

            return await dbContext.Set<TEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(entity => entity.Id == entityId);
        }

        private static DateTime AsUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}
