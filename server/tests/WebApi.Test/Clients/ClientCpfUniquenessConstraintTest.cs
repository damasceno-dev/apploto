using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Entities;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Clients;

/// <summary>
/// Verifies the hard PostgreSQL uniqueness enforcement for the filtered
/// <c>UNIQUE (BranchId, Cpf) WHERE Cpf IS NOT NULL AND Active = true</c> index on
/// <c>Clients</c> by attempting a raw duplicate insert directly through
/// <see cref="ServerDbContext"/> — bypassing all application-layer pre-checks.
///
/// This test is intentionally separate from the HTTP-level 409 tests in
/// <see cref="ClientControllerUnhappyPathTest"/>: those verify the application-layer
/// duplicate check that runs before <c>Commit()</c>. This test proves that the
/// underlying migration correctly applied the filtered index so that even a concurrent
/// race that slips past the pre-check is caught by the database.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ClientCpfUniquenessConstraintTest(ServerWebApplicationFactory factory)
{
    private const string PostgresUniqueViolationSqlState = "23505";

    [Fact]
    public async Task Client_ShouldRejectDuplicateCpfInSameBranch_AtDatabaseLevel()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("ClientCpfConstraint");

        // Seed a client with a known CPF directly through the seeder
        // (normalized, 11 digits — no punctuation)
        const string sharedCpf = "11122233344";
        await factory.SeedClientAsync(branch.Id, cpf: sharedCpf);

        // Attempt a second raw insert that reuses the same (BranchId, Cpf) pair,
        // bypassing the application-layer GetActiveByCpfAndBranchId pre-check.
        // If the migration applied the filtered index, SaveChangesAsync must reject it.
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var duplicate = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Duplicate CPF Client",
            Phone = "11999990000",
            Cpf = sharedCpf,
            BranchId = branch.Id
        };
        dbContext.Clients.Add(duplicate);

        var exception = await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        var postgresException = exception.InnerException.ShouldBeOfType<PostgresException>();
        postgresException.SqlState.ShouldBe(PostgresUniqueViolationSqlState);
        postgresException.ConstraintName.ShouldBe("IX_Clients_BranchId_Cpf");
    }

    [Fact]
    public async Task Client_ShouldAllowSameCpfInDifferentBranches_AtDatabaseLevel()
    {
        // The filtered index is scoped to (BranchId, Cpf). The same CPF must be
        // insertable when the BranchId differs — this verifies the scope of the index.
        var (_, branch1, _, _) = await factory.SeedFullBranchContextAsync("ClientCpfConstraintBranch1");
        var (_, branch2, _, _) = await factory.SeedFullBranchContextAsync("ClientCpfConstraintBranch2");

        const string sharedCpf = "99988877766";
        await factory.SeedClientAsync(branch1.Id, cpf: sharedCpf);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var crossBranchClient = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Cross-branch CPF Client",
            Phone = "11999990001",
            Cpf = sharedCpf,
            BranchId = branch2.Id
        };
        dbContext.Clients.Add(crossBranchClient);

        // Should NOT throw — different branch, same CPF is allowed.
        await Should.NotThrowAsync(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Client_ShouldAllowNullCpfMultipleTimes_InSameBranch_AtDatabaseLevel()
    {
        // The filtered index only applies WHERE Cpf IS NOT NULL. Multiple clients
        // with null CPF in the same branch must be insertable without conflict.
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync("ClientCpfConstraintNull");
        await factory.SeedClientAsync(branch.Id, cpf: null);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var secondNullCpf = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Second Null CPF Client",
            Phone = "11999990002",
            Cpf = null,
            BranchId = branch.Id
        };
        dbContext.Clients.Add(secondNullCpf);

        // Should NOT throw — null CPF is excluded from the unique index.
        await Should.NotThrowAsync(() => dbContext.SaveChangesAsync());
    }
}
