using System.Net;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Holidays;

[Collection(ServerApiCollection.Name)]
public class HolidayControllerImportBrazilianHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task PreviewBrazilianImport_ShouldReturn200WithTenNationalItems_WhenOptionalFederalIsFalse()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrPreviewFalse", Role.Member);
        await factory.SeedHolidayAsync(branch.Id, new DateTime(2026, 1, 1), "Existing");

        var httpResponse = await _client.GetAuthAsync("/holiday/import-br/2026/preview", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayPreviewJson>();
        payload.Year.ShouldBe(2026);
        payload.IncludesOptionalFederal.ShouldBeFalse();
        payload.Items.Count.ShouldBe(10);
        payload.Items.ShouldAllBe(item => item.Type == BrazilianHolidayType.National);
        payload.Items.Single(item => item.Date == new DateOnly(2026, 1, 1)).AlreadyExists.ShouldBeTrue();
    }

    [Fact]
    public async Task PreviewBrazilianImport_ShouldReturn200WithThirteenItems_WhenOptionalFederalIsTrue()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("HolBrPreviewTrue", Role.Manager);

        var httpResponse = await _client.GetAuthAsync(
            "/holiday/import-br/2026/preview?includeOptionalFederal=true",
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayPreviewJson>();
        payload.Year.ShouldBe(2026);
        payload.IncludesOptionalFederal.ShouldBeTrue();
        payload.Items.Count.ShouldBe(13);
        payload.Items.Count(item => item.Type == BrazilianHolidayType.National).ShouldBe(10);
        payload.Items.Count(item => item.Type == BrazilianHolidayType.OptionalFederal).ShouldBe(3);
        payload.Items.Select(item => item.Date).ShouldContain(new DateOnly(2026, 2, 17));
        payload.Items.Select(item => item.Date).ShouldContain(new DateOnly(2026, 6, 4));
    }

    [Fact]
    public async Task ImportBrazilian_ShouldPersistTenActiveHolidays_WhenOptionalFederalIsFalse()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportFalse", Role.Manager);

        var httpResponse = await _client.PostAuthAsync("/holiday/import-br/2027", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        payload.Items.Count.ShouldBe(10);
        payload.ImportedCount.ShouldBe(10);
        payload.SkippedCount.ShouldBe(0);
        payload.Items.ShouldAllBe(item => item.Status == BrazilianHolidayImportStatus.Imported);

        var persisted = await factory.ListActiveHolidaysByBranchIdAndYearAsync(branch.Id, 2027);
        persisted.Count.ShouldBe(10);
        persisted.Select(holiday => holiday.Date).ShouldContain(new DateTime(2027, 3, 26));
        persisted.Select(holiday => holiday.Date).ShouldNotContain(new DateTime(2027, 2, 9));
    }

    [Fact]
    public async Task ImportBrazilian_ShouldPersistThirteenActiveHolidays_WhenOptionalFederalIsTrue()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportTrue", Role.Admin);

        var httpResponse = await _client.PostAuthAsync(
            "/holiday/import-br/2028?includeOptionalFederal=true",
            token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        payload.Items.Count.ShouldBe(13);
        payload.ImportedCount.ShouldBe(13);
        payload.SkippedCount.ShouldBe(0);

        var persisted = await factory.ListActiveHolidaysByBranchIdAndYearAsync(branch.Id, 2028);
        persisted.Count.ShouldBe(13);
        persisted.Select(holiday => holiday.Date).ShouldContain(new DateTime(2028, 2, 29));
        persisted.Select(holiday => holiday.Date).ShouldContain(new DateTime(2028, 6, 15));
    }

    [Fact]
    public async Task ImportBrazilian_ShouldSkipPreExistingDateAndImportTheRest()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportConflict", Role.Manager);
        var existing = await factory.SeedHolidayAsync(branch.Id, new DateTime(2029, 1, 1), "Custom existing");

        var httpResponse = await _client.PostAuthAsync("/holiday/import-br/2029", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        payload.ImportedCount.ShouldBe(9);
        payload.SkippedCount.ShouldBe(1);
        payload.Items.Single(item => item.Date == new DateOnly(2029, 1, 1)).Status.ShouldBe(BrazilianHolidayImportStatus.Skipped);

        var persisted = await factory.ListActiveHolidaysByBranchIdAndYearAsync(branch.Id, 2029);
        persisted.Count.ShouldBe(10);
        persisted.Single(holiday => holiday.Id == existing.Id).Description.ShouldBe("Custom existing");
    }

    [Fact]
    public async Task ImportBrazilian_ShouldBeIdempotent_WhenCalledTwice()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("HolBrImportTwice", Role.Admin);

        var firstResponse = await _client.PostAuthAsync(
            "/holiday/import-br/2030?includeOptionalFederal=true",
            token);
        var secondResponse = await _client.PostAuthAsync(
            "/holiday/import-br/2030?includeOptionalFederal=true",
            token);

        firstResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        secondResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var firstPayload = await firstResponse.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        var secondPayload = await secondResponse.ReadContentAsync<ResponseBrazilianHolidayImportJson>();
        firstPayload.ImportedCount.ShouldBe(13);
        secondPayload.ImportedCount.ShouldBe(0);
        secondPayload.SkippedCount.ShouldBe(13);
        secondPayload.Items.ShouldAllBe(item => item.Status == BrazilianHolidayImportStatus.Skipped);

        var persisted = await factory.ListActiveHolidaysByBranchIdAndYearAsync(branch.Id, 2030);
        persisted.Count.ShouldBe(13);
    }
}
