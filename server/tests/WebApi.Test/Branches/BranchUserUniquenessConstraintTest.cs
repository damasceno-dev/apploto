using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Branches;

/// <summary>
/// Verifies the hard PostgreSQL uniqueness enforcement for <c>BranchUser (UserId, BranchId)</c>
/// by attempting a raw duplicate insert through <see cref="ServerDbContext"/> and asserting that
/// the database — not the application layer — is the one rejecting the write. This test is
/// deliberately separate from the use-case-level reactivation tests (milestone 4.8): those assert
/// the business-layer behavior with mocked I/O, while this test assures that the underlying
/// schema migration creates the unique index that the use case relies on.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class BranchUserUniquenessConstraintTest(ServerWebApplicationFactory factory)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    [Fact]
    public async Task BranchUser_ShouldReject_DuplicateUserIdAndBranchIdPair_AtDatabaseLevel()
    {
        var (user, branch, _, _) = await factory.SeedFullBranchContextAsync("UniquenessConstraint");

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        // Build a second BranchUser entity that reuses the same (UserId, BranchId) pair
        // as the seeded membership. If the migration applied the expected unique index,
        // the insert must fail before any application-layer validation runs.
        var duplicate = new BranchUser
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            BranchId = branch.Id,
            Role = Role.Member
        };
        dbContext.BranchUsers.Add(duplicate);

        var exception = await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        var postgresException = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresException.SqlState.ShouldBe(PostgresUniqueViolationSqlState);
    }
}
