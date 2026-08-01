using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.ExceptionHandling;
using server.Exceptions;
using server.Exceptions.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.ExceptionHandling;

[Collection(ServerApiCollection.Name)]
public class PostgresConcurrencyTranslationIntegrationTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task DailyCloseXminRace_ShouldThrowAndTranslateToDailyCloseConflict()
    {
        var (_, branch, _, _) = await factory.SeedFullBranchContextAsync(
            "DailyCloseXminRace",
            Role.Manager);
        var account = await factory.SeedAccountAsync(branch.Id, AccountType.Terminal);
        var close = await factory.SeedDailyCloseAsync(branch.Id, account.Id);

        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var firstCopy = await firstContext.DailyCloses.SingleAsync(row => row.Id == close.Id);
        var secondCopy = await secondContext.DailyCloses.SingleAsync(row => row.Id == close.Id);
        firstCopy.Version.ShouldBe(secondCopy.Version);

        firstCopy.Notes = "first concurrent write";
        await firstContext.SaveChangesAsync();
        secondCopy.Notes = "stale concurrent write";

        var exception = await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync());

        exception.Entries.ShouldHaveSingleItem().Entity.ShouldBeOfType<DailyClose>();
        var normalized = PostgresExceptionHandler.Normalize(exception)
            .ShouldBeOfType<ConflictException>();
        normalized.Message.ShouldBe(ResourcesErrorMessages.DAILYCLOSE_STALE_WRITE);
        var persisted = await factory.ReloadAsync<DailyClose>(close.Id);
        persisted.ShouldNotBeNull();
        persisted.Notes.ShouldBe("first concurrent write");
    }

    [Fact]
    public async Task RefreshTokenDeleteRace_ShouldNotTranslateToDailyCloseConflict()
    {
        var user = await factory.SeedUserAsync();
        var refreshToken = await factory.SeedRefreshTokenAsync(user.Id);

        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var firstContext = firstScope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var secondContext = secondScope.ServiceProvider.GetRequiredService<ServerDbContext>();
        var firstCopy = await firstContext.RefreshTokens.SingleAsync(row => row.Id == refreshToken.Id);
        var secondCopy = await secondContext.RefreshTokens.SingleAsync(row => row.Id == refreshToken.Id);

        firstContext.RefreshTokens.Remove(firstCopy);
        secondContext.RefreshTokens.Remove(secondCopy);
        await firstContext.SaveChangesAsync();

        var exception = await Should.ThrowAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync());

        exception.Entries.ShouldHaveSingleItem().Entity.ShouldBeOfType<RefreshToken>();
        PostgresExceptionHandler.Normalize(exception).ShouldBeSameAs(exception);
    }
}
