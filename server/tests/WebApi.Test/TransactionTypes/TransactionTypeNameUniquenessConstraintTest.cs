using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.TransactionTypes;

/// <summary>
/// Verifies the hard PostgreSQL uniqueness enforcement for the filtered
/// <c>UNIQUE (CategoryId, Name) WHERE Active = true</c> index on <c>TransactionTypes</c>
/// by attempting a raw duplicate insert directly through <see cref="ServerDbContext"/> —
/// bypassing all application-layer pre-checks.
///
/// Constraint-level test — proves that the M6 Phase 1 migration applied the filter so
/// duplicates among active rows are rejected, but the same name is reusable once the
/// prior row has been deactivated.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class TransactionTypeNameUniquenessConstraintTest(ServerWebApplicationFactory factory)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    [Fact]
    public async Task TransactionType_ShouldRejectDuplicateActiveNameInSameCategory_AtDatabaseLevel()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("TransactionTypeNameConstraint");
        var category = await factory.SeedCategoryAsync(branch.Id);

        const string sharedName = "Mega-Sena";
        await factory.SeedTransactionTypeAsync(category.Id, name: sharedName);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var duplicate = new TransactionType
        {
            Id = Guid.NewGuid(),
            Name = sharedName,
            CategoryId = category.Id,
            SettlementRule = SettlementRule.SameDay,
            RequiresTabAccountAndClient = false
        };
        dbContext.TransactionTypes.Add(duplicate);

        var exception = await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        var postgresException = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresException.SqlState.ShouldBe(PostgresUniqueViolationSqlState);
        postgresException.ConstraintName.ShouldBe("IX_TransactionTypes_CategoryId_Name");
    }

    [Fact]
    public async Task TransactionType_ShouldAllowDuplicateNameWhenPriorRowInactive_AtDatabaseLevel()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("TransactionTypeNameConstraintInactive");
        var category = await factory.SeedCategoryAsync(branch.Id);

        const string sharedName = "Quina";
        await factory.SeedTransactionTypeAsync(category.Id, name: sharedName, active: false);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var reactivated = new TransactionType
        {
            Id = Guid.NewGuid(),
            Name = sharedName,
            CategoryId = category.Id,
            SettlementRule = SettlementRule.SameDay,
            RequiresTabAccountAndClient = false
        };
        dbContext.TransactionTypes.Add(reactivated);

        await Should.NotThrowAsync(() => dbContext.SaveChangesAsync());
    }
}
