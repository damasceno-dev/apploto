using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Categories;

/// <summary>
/// Verifies the hard PostgreSQL uniqueness enforcement for the filtered
/// <c>UNIQUE (BranchId, Name) WHERE Active = true</c> index on <c>Categories</c>
/// by attempting a raw duplicate insert directly through <see cref="ServerDbContext"/> —
/// bypassing all application-layer pre-checks.
///
/// Constraint-level test — proves that the M6 Phase 1 migration applied the filter so
/// duplicates among active rows are rejected, but the same name is reusable once the
/// prior row has been deactivated.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class CategoryNameUniquenessConstraintTest(ServerWebApplicationFactory factory)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    [Fact]
    public async Task Category_ShouldRejectDuplicateActiveNameInSameBranch_AtDatabaseLevel()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("CategoryNameConstraint");

        const string sharedName = "Loterias";
        await factory.SeedCategoryAsync(branch.Id, name: sharedName);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var duplicate = new Category
        {
            Id = Guid.NewGuid(),
            Name = sharedName,
            DefaultDirection = Direction.In,
            BranchId = branch.Id
        };
        dbContext.Categories.Add(duplicate);

        var exception = await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        var postgresException = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresException.SqlState.ShouldBe(PostgresUniqueViolationSqlState);
        postgresException.ConstraintName.ShouldBe("IX_Categories_BranchId_Name");
    }

    [Fact]
    public async Task Category_ShouldAllowDuplicateNameWhenPriorRowInactive_AtDatabaseLevel()
    {
        // The filter excludes Active = false rows, so a deactivated row must not block
        // the creation of a fresh active row with the same name in the same branch.
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("CategoryNameConstraintInactive");

        const string sharedName = "Bolão";

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var inactive = new Category
        {
            Id = Guid.NewGuid(),
            Name = sharedName,
            DefaultDirection = Direction.In,
            BranchId = branch.Id,
            Active = false
        };
        dbContext.Categories.Add(inactive);
        await dbContext.SaveChangesAsync();

        var reactivated = new Category
        {
            Id = Guid.NewGuid(),
            Name = sharedName,
            DefaultDirection = Direction.In,
            BranchId = branch.Id
        };
        dbContext.Categories.Add(reactivated);

        await Should.NotThrowAsync(() => dbContext.SaveChangesAsync());
    }
}
