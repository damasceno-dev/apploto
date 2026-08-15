using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerImportBrazilianUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ImportBrazilian_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrImport403", Role.Member);

        var httpResponse = await _client.PostAuthAsync("/holiday/import-br/2026", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
    }

    [Fact]
    public async Task PreviewBrazilianImport_ShouldReturn200_WhenCallerIsMember()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrPreviewMember", Role.Member);

        var httpResponse = await _client.GetAuthAsync(
            "/holiday/import-br/2026/preview?includeOptionalFederal=true",
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayPreviewJson>();
        payload.Items.Count.ShouldBe(13);
    }

    [Fact]
    public async Task PreviewBrazilianImport_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/holiday/import-br/2026/preview");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task ImportBrazilian_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.PostAsync("/holiday/import-br/2026", content: null);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Theory]
    [InlineData("/holiday/import-br/1800/preview")]
    [InlineData("/holiday/import-br/2300")]
    public async Task BrazilianImportRoutes_ShouldReturn404_WhenYearViolatesRouteConstraint(string uri)
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync($"HolBrRoute404{Guid.NewGuid():N}", Role.Admin);

        var httpResponse = uri.EndsWith("/preview", StringComparison.Ordinal)
            ? await _client.GetAuthAsync(uri, token)
            : await _client.PostAuthAsync(uri, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // -------------------------------------------------------------------------
    // 400 — query-string model binding rejects invalid enum values for ?source.
    // Reachable even though the route's year constraint guards 1900..2200.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PreviewBrazilianImport_ShouldReturn400_WhenSourceQueryStringIsInvalidEnumValue()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrPreview400InvalidSource");

        var httpResponse = await _client.GetAuthAsync(
            "/holiday/import-br/2026/preview?source=NotAValidSource",
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ImportBrazilian_ShouldReturn400_WhenSourceQueryStringIsInvalidEnumValue()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrImport400InvalidSource", Role.Admin);

        var httpResponse = await _client.PostAuthAsync(
            "/holiday/import-br/2026?source=NotAValidSource",
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // 409 — filtered-unique race-condition path. The import use case pre-reads
    // existing dates and skips matching dates, so a single-call test cannot
    // naturally produce 409. We simulate the race window with a decorator
    // around the real HolidaysRepository: it returns an empty
    // ListActiveDatesByBranchIdAndYearAsNoTracking (lying as if no row exists
    // yet) while the underlying database actually has an active row on the
    // import target date. The use case therefore stages an insert, UnitOfWork
    // commit hits the IX_Holidays_BranchId_Date filtered unique violation,
    // and PostgresExceptionHandler translates the 23505 SqlState into
    // ConflictException → 409 HOLIDAY_DATE_CONFLICT.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ImportBrazilian_ShouldReturn409_WhenConcurrentInsertWinsTheRaceForCanonicalDate()
    {
        const int targetYear = 2042;
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrImport409Race", Role.Admin);

        // Pre-seed an active holiday on Jan 1 (the CONFRATERNIZACAO_UNIVERSAL canonical date)
        // so the real database has the row even though the decorated repository will hide it
        // from the use case's existing-dates pre-check.
        await factory.SeedHolidayAsync(branch.Id, new DateTime(targetYear, 1, 1), "pre-existing");

        await using var raceFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHolidaysRepository>();
                services.AddScoped<server.Infrastructure.Repositories.HolidaysRepository>();
                services.AddScoped<IHolidaysRepository>(sp => new RaceWindowHolidaysRepository(
                    sp.GetRequiredService<server.Infrastructure.Repositories.HolidaysRepository>()));
            });
        });
        var raceClient = raceFactory.CreateClient();

        var httpResponse = await raceClient.PostAuthAsync($"/holiday/import-br/{targetYear}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT);
    }

    /// <summary>
    /// Decorator around the real <see cref="server.Infrastructure.Repositories.HolidaysRepository"/>
    /// that lies about which dates already exist for a given year — returning an empty
    /// list so the import use case proceeds with inserts. All other repository calls
    /// pass through unchanged. Simulates the concurrent-write race window between
    /// the existing-dates pre-check and the unit-of-work commit.
    /// </summary>
    private sealed class RaceWindowHolidaysRepository(IHolidaysRepository inner) : IHolidaysRepository
    {
        public Task Add(Holiday holiday) => inner.Add(holiday);
        public Task<Holiday?> GetByIdAndBranchId(Guid id, Guid branchId) => inner.GetByIdAndBranchId(id, branchId);
        public Task<Holiday?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId) => inner.GetByIdAndBranchIdAsNoTracking(id, branchId);
        public Task<Holiday?> GetActiveByBranchIdAndDate(Guid branchId, DateTime date) => inner.GetActiveByBranchIdAndDate(branchId, date);
        public Task<IReadOnlyList<Holiday>> ListByBranchIdAsNoTracking(Guid branchId, HolidayListFilter filter) => inner.ListByBranchIdAsNoTracking(branchId, filter);
        public Task<int> CountByBranchIdAsNoTracking(Guid branchId, HolidayListFilter filter) => inner.CountByBranchIdAsNoTracking(branchId, filter);
        public Task<IReadOnlyList<DateOnly>> ListActiveDatesByBranchIdAsNoTracking(
            Guid branchId,
            CancellationToken ct = default) => inner.ListActiveDatesByBranchIdAsNoTracking(branchId, ct);
        public Task<IReadOnlyList<DateOnly>> ListActiveDatesByBranchIdAndYearAsNoTracking(Guid branchId, int year)
            => Task.FromResult<IReadOnlyList<DateOnly>>([]);
    }
}
