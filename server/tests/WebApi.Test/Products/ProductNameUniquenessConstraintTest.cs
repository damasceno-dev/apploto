using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Entities;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Products;

/// <summary>
/// Verifies the hard PostgreSQL uniqueness enforcement for the filtered
/// <c>UNIQUE (BranchId, Name) WHERE Active = true</c> index on <c>Products</c>
/// by attempting a raw duplicate insert directly through <see cref="ServerDbContext"/> —
/// bypassing all application-layer pre-checks.
///
/// Constraint-level test — proves that the M6 Phase 1 migration applied the filter so
/// duplicates among active rows are rejected, but the same name is reusable once the
/// prior row has been deactivated.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ProductNameUniquenessConstraintTest(ServerWebApplicationFactory factory)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    [Fact]
    public async Task Product_ShouldRejectDuplicateActiveNameInSameBranch_AtDatabaseLevel()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("ProductNameConstraint");

        const string sharedName = "Raspadinha";
        await factory.SeedProductAsync(branch.Id, name: sharedName);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var duplicate = new Product
        {
            Id = Guid.NewGuid(),
            Name = sharedName,
            DisplayOrder = 5,
            BranchId = branch.Id
        };
        dbContext.Products.Add(duplicate);

        var exception = await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        var postgresException = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresException.SqlState.ShouldBe(PostgresUniqueViolationSqlState);
        postgresException.ConstraintName.ShouldBe("IX_Products_BranchId_Name");
    }

    [Fact]
    public async Task Product_ShouldAllowDuplicateNameWhenPriorRowInactive_AtDatabaseLevel()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("ProductNameConstraintInactive");

        const string sharedName = "Lotofácil";

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var inactive = new Product
        {
            Id = Guid.NewGuid(),
            Name = sharedName,
            DisplayOrder = 1,
            BranchId = branch.Id,
            Active = false
        };
        dbContext.Products.Add(inactive);
        await dbContext.SaveChangesAsync();

        var reactivated = new Product
        {
            Id = Guid.NewGuid(),
            Name = sharedName,
            DisplayOrder = 2,
            BranchId = branch.Id
        };
        dbContext.Products.Add(reactivated);

        await Should.NotThrowAsync(() => dbContext.SaveChangesAsync());
    }
}
