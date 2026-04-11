using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Infrastructure;

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

        public async Task<Branch> SeedBranchAsync(string? name = null)
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

        /// <summary>
        /// Reloads an entity from the database so tests can assert persisted state without
        /// relying on cached tracking from the seed calls.
        /// </summary>
        public async Task<BranchUser?> ReloadBranchUserAsync(Guid branchUserId)
        {
            using var scope = factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            return await dbContext.BranchUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(branchUser => branchUser.Id == branchUserId);
        }
    }
}
