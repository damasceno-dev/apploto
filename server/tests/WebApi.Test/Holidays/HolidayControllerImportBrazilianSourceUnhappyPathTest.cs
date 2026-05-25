using System.Net;
using CommonTestUtilities.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces.Holidays;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

/// <summary>
/// Integration tests for the Phase 6.5 multi-source dispatch's failure surfaces:
/// explicit single-source calls produce 502 + <c>HOLIDAY_SOURCE_UNAVAILABLE</c> when
/// the provider returns a failed result; composite never surfaces 502 even when
/// both providers fail simultaneously.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class HolidayControllerImportBrazilianSourceUnhappyPathTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task ImportBrazilian_ExplicitNager_ShouldReturn502_WhenFakeProviderReturnsFailedResult()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportNager502", Role.Admin);

        var fakeNager = new NagerDateHolidayProviderBuilder()
            .ReturnsFailureForYear(2037, "Nager.Date request timed out")
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

        var httpResponse = await customClient.PostAuthAsync("/holiday/import-br/2037?source=Nager", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_SOURCE_UNAVAILABLE);
    }

    [Fact]
    public async Task ImportBrazilian_ExplicitBrasilApi_ShouldReturn502_WhenFakeProviderReturnsFailedResult()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportBrasilApi502", Role.Admin);

        var fakeBrasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsFailureForYear(2038, "BrasilAPI returned non-success status 503")
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

        var httpResponse = await customClient.PostAuthAsync("/holiday/import-br/2038?source=BrasilApi", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_SOURCE_UNAVAILABLE);
    }

    [Fact]
    public async Task PreviewBrazilianImport_ExplicitNager_ShouldReturn502_WhenFakeProviderReturnsFailedResult()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrPreviewNager502", Role.Member);

        var fakeNager = new NagerDateHolidayProviderBuilder()
            .ReturnsFailureForYear(2039, "Nager.Date is down")
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

        var httpResponse = await customClient.GetAuthAsync("/holiday/import-br/2039/preview?source=Nager", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadGateway);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.HOLIDAY_SOURCE_UNAVAILABLE);
    }

    [Fact]
    public async Task ImportBrazilian_Composite_ShouldNeverReturn502_EvenWhenBothFakeProvidersFail()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportCompositeNo502", Role.Admin);

        var fakeBrasilApi = new BrasilApiHolidayProviderBuilder()
            .ReturnsFailureForYear(2040, "BrasilAPI request timed out")
            .Build();
        var fakeNager = new NagerDateHolidayProviderBuilder()
            .ReturnsFailureForYear(2040, "Nager.Date is down")
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

        var httpResponse = await customClient.PostAuthAsync("/holiday/import-br/2040?source=Composite", token);

        // Composite is guaranteed to succeed because canonical backfills every concept.
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ImportBrazilian_ExplicitNager_ShouldReturn403_WhenCallerIsMember_AndProviderFakeIsNeverInvoked()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportNager403Member", Role.Member);

        var fakeNager = new NagerDateHolidayProviderBuilder()
            .ReturnsFailureForYear(2041, "must not be reached")
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

        var httpResponse = await customClient.PostAuthAsync("/holiday/import-br/2041?source=Nager", token);

        // Role guard must fire before the resolver runs — 403 must not be masked by 502.
        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
