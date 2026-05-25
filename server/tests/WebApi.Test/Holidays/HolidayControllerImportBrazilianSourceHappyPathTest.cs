using System.Net;
using CommonTestUtilities.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces.Holidays;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

/// <summary>
/// Integration tests for the Phase 6.5 multi-source dispatch: composite default
/// with mixed provenance, explicit single-source happy paths, idempotent re-runs,
/// and persistence-level assertions on <c>Holiday.Source</c>. External provider
/// HTTP clients are replaced with NSubstitute fakes via
/// <see cref="WithWebHostBuilderExtensions"/> so the suite never reaches BrasilAPI
/// or Nager.Date over the network.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class HolidayControllerImportBrazilianSourceHappyPathTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task ImportBrazilian_Composite_ShouldPersistMixedSourceProvenance_WhenBothProvidersClaimDistinctConcepts()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportCompositeMixed", Role.Manager);

        // Nager claims Jan 1 and Apr 21; BrasilAPI claims May 1 and Dec 25 (Nager doesn't include them
        // in this fixture). Every other concept falls through to canonical backfill.
        var fakeNager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2031, [
                new NagerDateHolidayDto("2031-01-01", "Confraternização Universal", "New Year's Day", "BR",
                    Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
                new NagerDateHolidayDto("2031-04-21", "Dia de Tiradentes", "Tiradentes", "BR",
                    Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"])
            ])
            .Build();
        var fakeBrasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsSuccessForYear(2031, [
                new BrasilApiHolidayDto("2031-05-01", "Dia do Trabalho", "national", "quinta-feira"),
                new BrasilApiHolidayDto("2031-12-25", "Natal", "national", "quinta-feira")
            ])
            .Build();

        await using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBrasilApiHolidayProvider>();
                services.AddSingleton(fakeBrasilApi);
                services.RemoveAll<INagerDateHolidayProvider>();
                services.AddSingleton(fakeNager);
            });
        });
        var customClient = customFactory.CreateClient();

        var httpResponse = await customClient.PostAuthAsync("/holiday/import-br/2031", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        payload.Year.ShouldBe(2031);
        payload.Source.ShouldBe(BrazilianHolidayCalendarSource.Composite);
        payload.Items.Count.ShouldBe(10);
        payload.ImportedCount.ShouldBe(10);

        // Reload persisted rows to assert the Holiday.Source column reflects the per-concept claim.
        var persisted = await factory.ListActiveHolidaysByBranchIdAndYearAsync(branch.Id, 2031);
        persisted.Count.ShouldBe(10);
        persisted.Single(h => h.Date == new DateTime(2031, 1, 1)).Source.ShouldBe(HolidaySource.Nager);
        persisted.Single(h => h.Date == new DateTime(2031, 4, 21)).Source.ShouldBe(HolidaySource.Nager);
        persisted.Single(h => h.Date == new DateTime(2031, 5, 1)).Source.ShouldBe(HolidaySource.BrasilApi);
        persisted.Single(h => h.Date == new DateTime(2031, 12, 25)).Source.ShouldBe(HolidaySource.BrasilApi);
        // Concepts neither provider claimed are canonical backfill.
        persisted.Single(h => h.Date == new DateTime(2031, 9, 7)).Source.ShouldBe(HolidaySource.Canonical);
    }

    [Fact]
    public async Task ImportBrazilian_ExplicitNager_ShouldReturn200_WhenFakeProviderSucceeds()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportExplicitNager", Role.Admin);

        var fakeNager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2032, [
                new NagerDateHolidayDto("2032-01-01", "Confraternização Universal", "New Year's Day", "BR",
                    Fixed: false, Global: true, Counties: null, LaunchYear: null, Types: ["Public"]),
                new NagerDateHolidayDto("2032-12-25", "Natal", "Christmas Day", "BR",
                    Fixed: true, Global: true, Counties: null, LaunchYear: null, Types: ["Public"])
            ])
            .Build();

        await using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<INagerDateHolidayProvider>();
                services.AddSingleton(fakeNager);
            });
        });
        var customClient = customFactory.CreateClient();

        var httpResponse = await customClient.PostAuthAsync("/holiday/import-br/2032?source=Nager", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        payload.Source.ShouldBe(BrazilianHolidayCalendarSource.Nager);
        payload.Items.Count.ShouldBe(10);

        var persisted = await factory.ListActiveHolidaysByBranchIdAndYearAsync(branch.Id, 2032);
        persisted.Single(h => h.Date == new DateTime(2032, 1, 1)).Source.ShouldBe(HolidaySource.Nager);
        persisted.Single(h => h.Date == new DateTime(2032, 12, 25)).Source.ShouldBe(HolidaySource.Nager);
        // Unclaimed concepts (everything except the two Nager fixtures) are canonical backfill.
        persisted.Count(h => h.Source == HolidaySource.Canonical).ShouldBe(8);
    }

    [Fact]
    public async Task ImportBrazilian_ExplicitBrasilApi_ShouldReturn200_WhenFakeProviderSucceeds()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportExplicitBrasilApi", Role.Admin);

        var fakeBrasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsSuccessForYear(2033, [
                new BrasilApiHolidayDto("2033-01-01", "Confraternização mundial", "national", "sábado"),
                new BrasilApiHolidayDto("2033-04-21", "Tiradentes", "national", "quinta-feira"),
                new BrasilApiHolidayDto("2033-12-25", "Natal", "national", "domingo")
            ])
            .Build();

        await using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBrasilApiHolidayProvider>();
                services.AddSingleton(fakeBrasilApi);
            });
        });
        var customClient = customFactory.CreateClient();

        var httpResponse = await customClient.PostAuthAsync("/holiday/import-br/2033?source=BrasilApi", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        payload.Source.ShouldBe(BrazilianHolidayCalendarSource.BrasilApi);
        // BrasilAPI surface description for Confraternização Universal is "Confraternização mundial"
        // — preserved as the persisted Description.
        payload.Items.Single(i => i.Date == new DateOnly(2033, 1, 1)).Description.ShouldBe("Confraternização mundial");

        var persisted = await factory.ListActiveHolidaysByBranchIdAndYearAsync(branch.Id, 2033);
        persisted.Single(h => h.Date == new DateTime(2033, 1, 1)).Source.ShouldBe(HolidaySource.BrasilApi);
        persisted.Single(h => h.Date == new DateTime(2033, 4, 21)).Source.ShouldBe(HolidaySource.BrasilApi);
        persisted.Single(h => h.Date == new DateTime(2033, 12, 25)).Source.ShouldBe(HolidaySource.BrasilApi);
    }

    [Fact]
    public async Task ImportBrazilian_Composite_ShouldFallBackEntirelyToCanonical_WhenBothFakeProvidersFail()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportCompositeAllFail", Role.Manager);

        var fakeBrasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsFailureForYear(2034, "BrasilAPI is down")
            .Build();
        var fakeNager = new NagerDateHolidayProviderBuilder()
            .ReturnsFailureForYear(2034, "Nager.Date is down")
            .Build();

        await using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IBrasilApiHolidayProvider>();
                services.AddSingleton(fakeBrasilApi);
                services.RemoveAll<INagerDateHolidayProvider>();
                services.AddSingleton(fakeNager);
            });
        });
        var customClient = customFactory.CreateClient();

        var httpResponse = await customClient.PostAuthAsync("/holiday/import-br/2034?includeOptionalFederal=true", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        payload.Items.Count.ShouldBe(13);
        payload.Items.ShouldAllBe(i => i.Source == HolidaySource.Canonical);

        var persisted = await factory.ListActiveHolidaysByBranchIdAndYearAsync(branch.Id, 2034);
        persisted.Count.ShouldBe(13);
        persisted.ShouldAllBe(h => h.Source == HolidaySource.Canonical);
    }

    [Fact]
    public async Task ImportBrazilian_Composite_ShouldBeIdempotent_AcrossSourceVariation()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportCompositeIdempotent", Role.Admin);

        var fakeNager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2035, [
                new NagerDateHolidayDto("2035-12-25", "Natal", "Christmas Day", "BR",
                    Fixed: true, Global: true, Counties: null, LaunchYear: null, Types: ["Public"])
            ])
            .Build();

        await using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<INagerDateHolidayProvider>();
                services.AddSingleton(fakeNager);
            });
        });
        var customClient = customFactory.CreateClient();

        var first = await customClient.PostAuthAsync("/holiday/import-br/2035", token);
        var second = await customClient.PostAuthAsync("/holiday/import-br/2035", token);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPayload = await first.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        var secondPayload = await second.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        firstPayload.ImportedCount.ShouldBe(10);
        secondPayload.ImportedCount.ShouldBe(0);
        secondPayload.SkippedCount.ShouldBe(10);
        secondPayload.Items.ShouldAllBe(i => i.Status == BrazilianHolidayImportStatus.Skipped);

        var persisted = await factory.ListActiveHolidaysByBranchIdAndYearAsync(branch.Id, 2035);
        persisted.Count.ShouldBe(10);
        persisted.Single(h => h.Date == new DateTime(2035, 12, 25)).Source.ShouldBe(HolidaySource.Nager);
    }

    [Fact]
    public async Task PreviewBrazilianImport_Composite_ShouldEchoTopLevelSource_AndExposePerRowSource()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrPreviewCompositeMixed", Role.Member);

        var fakeNager = new NagerDateHolidayProviderBuilder()
            .ReturnsSuccessForYear(2036, [
                new NagerDateHolidayDto("2036-12-25", "Natal", "Christmas Day", "BR",
                    Fixed: true, Global: true, Counties: null, LaunchYear: null, Types: ["Public"])
            ])
            .Build();

        await using var customFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<INagerDateHolidayProvider>();
                services.AddSingleton(fakeNager);
            });
        });
        var customClient = customFactory.CreateClient();

        var httpResponse = await customClient.GetAuthAsync("/holiday/import-br/2036/preview", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayPreviewJson>();
        payload.Source.ShouldBe(BrazilianHolidayCalendarSource.Composite);
        payload.Items.Single(i => i.Date == new DateOnly(2036, 12, 25)).Source.ShouldBe(HolidaySource.Nager);
        payload.Items.Where(i => i.Date != new DateOnly(2036, 12, 25)).ShouldAllBe(i => i.Source == HolidaySource.Canonical);
    }
}
