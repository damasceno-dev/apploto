using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.DailyCloses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Infrastructure;
using Shouldly;
using Xunit;

namespace WebApi.Test.Infrastructure;

[Collection(ServerApiCollection.Name)]
public class DailyClosePhase3PersistenceTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task MigratedDatabase_ShouldContainEveryPhase3ColumnAndRestrictedForeignKey()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        await dbContext.Database.GetPendingMigrationsAsync().ShouldBeEmptyAsync();

        var connection = dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.columns
            WHERE table_name = 'DailyCloses'
              AND column_name IN (
                  'OpenedByUserId',
                  'RecordedByUserId',
                  'RecordedByOperatorId',
                  'SubmittedByUserId',
                  'ItemsFirstRecordedAt',
                  'OpeningRecheckRequiredAt',
                  'OpeningRecheckTriggeredByDailyCloseId',
                  'OpeningRecheckTriggeredByUserId')
            """;
        Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(8);

        command.CommandText = """
            SELECT COUNT(*)
            FROM pg_constraint
            WHERE conrelid = '"DailyCloses"'::regclass
              AND contype = 'f'
              AND confdeltype = 'r'
              AND conname IN (
                  'FK_DailyCloses_Users_OpenedByUserId',
                  'FK_DailyCloses_Users_RecordedByUserId',
                  'FK_DailyCloses_Operators_RecordedByOperatorId',
                  'FK_DailyCloses_Users_SubmittedByUserId',
                  'FK_DailyCloses_DailyCloses_OpeningRecheckTriggeredByDailyClose~',
                  'FK_DailyCloses_Users_OpeningRecheckTriggeredByUserId')
            """;
        Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(6);

        command.CommandText = """
            SELECT COUNT(*)
            FROM pg_constraint
            WHERE conrelid = '"DailyCloses"'::regclass
              AND contype = 'c'
              AND conname = 'CK_DailyCloses_RecordingIdentityMatchesFirstCount'
            """;
        Convert.ToInt32(await command.ExecuteScalarAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task RecordingIdentityConstraint_ShouldRequireRecorderUserExactlyWhenFirstCountExists()
    {
        var (user, branch, _, _) = await factory.SeedFullBranchContextAsync(
            "DcPersistenceRecordingIdentity",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var uncounted = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            new DateTime(2026, 4, 1),
            itemsRecorded: false);
        var counted = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            new DateTime(2026, 4, 2),
            itemsRecorded: true,
            recordedByUserId: user.Id,
            recordedByOperatorId: null);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        var trackedUncounted = await dbContext.DailyCloses.SingleAsync(close => close.Id == uncounted.Id);
        trackedUncounted.RecordedByUserId = user.Id;
        await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        dbContext.ChangeTracker.Clear();
        var trackedCounted = await dbContext.DailyCloses.SingleAsync(close => close.Id == counted.Id);
        trackedCounted.RecordedByUserId = null;
        await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task OpeningQueries_ShouldShareCountedEligibilityAndMirroredOrdering()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync(
            "DcPersistenceEligibility",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var product = await factory.SeedProductAsync(branch.Id);
        var cashVariance = await factory.SeedProductAsync(
            branch.Id,
            CashVarianceProductResolver.CashVarianceProductName);
        var baseDate = new DateTime(2026, 7, 1);
        var explicitEmpty = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            baseDate.AddDays(1),
            itemsRecorded: true);
        var zeroCount = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            baseDate.AddDays(2),
            itemsRecorded: true);
        await factory.SeedDailyCloseItemAsync(zeroCount.Id, product.Id, 0m);
        var neverCountedCashVarianceOnly = await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            baseDate.AddDays(3),
            itemsRecorded: false);
        await factory.SeedDailyCloseItemAsync(
            neverCountedCashVarianceOnly.Id,
            cashVariance.Id,
            999m);

        using var scope = factory.Services.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDailyClosesRepository>();

        var firstForward = await repository.GetNextEligibleAfterDateByBranchIdAndAccountId(
            branch.Id,
            account.Id,
            baseDate);
        var secondForward = await repository.GetNextEligibleAfterDateByBranchIdAndAccountId(
            branch.Id,
            account.Id,
            explicitEmpty.Date);
        var backward = await repository.GetMostRecentBeforeDateByBranchIdAndAccountIdAsNoTracking(
            branch.Id,
            account.Id,
            baseDate.AddDays(4));

        firstForward.ShouldNotBeNull();
        firstForward.Id.ShouldBe(explicitEmpty.Id);
        secondForward.ShouldNotBeNull();
        secondForward.Id.ShouldBe(zeroCount.Id);
        backward.ShouldNotBeNull();
        backward.Id.ShouldBe(zeroCount.Id);
    }

    [Fact]
    public async Task OpeningRecheckSourceForeignKey_ShouldRestrictPhysicalDelete()
    {
        var (user, branch, _, _) = await factory.SeedFullBranchContextAsync(
            "DcPersistenceSourceFk",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var source = await factory.SeedDailyCloseAsync(branch.Id, account.Id, new DateTime(2026, 6, 1));
        await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            new DateTime(2026, 6, 2),
            openingRecheckRequiredAt: DateTime.UtcNow,
            openingRecheckTriggeredByDailyCloseId: source.Id,
            openingRecheckTriggeredByUserId: user.Id);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var trackedSource = await dbContext.DailyCloses.SingleAsync(close => close.Id == source.Id);
        dbContext.DailyCloses.Remove(trackedSource);

        await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task OpeningRecheckUserForeignKey_ShouldRestrictPhysicalDelete()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync(
            "DcPersistenceUserFkBranch",
            Role.Manager);
        var triggeringUser = await factory.SeedUserAsync();
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var source = await factory.SeedDailyCloseAsync(branch.Id, account.Id, new DateTime(2026, 5, 1));
        await factory.SeedDailyCloseAsync(
            branch.Id,
            account.Id,
            new DateTime(2026, 5, 2),
            openingRecheckRequiredAt: DateTime.UtcNow,
            openingRecheckTriggeredByDailyCloseId: source.Id,
            openingRecheckTriggeredByUserId: triggeringUser.Id);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var trackedUser = await dbContext.Users.SingleAsync(user => user.Id == triggeringUser.Id);
        dbContext.Users.Remove(trackedUser);

        await Should.ThrowAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }
}

internal static class ShouldlyTaskExtensions
{
    public static async Task ShouldBeEmptyAsync(this Task<IEnumerable<string>> source)
    {
        (await source).ShouldBeEmpty();
    }
}
