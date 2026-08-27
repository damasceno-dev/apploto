using System.Net;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Serialization;

[Collection(ServerApiCollection.Name)]
public sealed class NamedEnumRequestValidationTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CategoryRequest_ShouldRetainFeatureMessageForUndefinedIntegerDirection()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("InvalidCategoryEnum", Role.Admin);
        var request = new
        {
            name = $"Invalid category direction {Guid.NewGuid():N}",
            defaultDirection = 999
        };

        using var response = await _client.PostAuthAsync("/category", request, token);

        await AssertInvalidEnum(response, ResourcesErrorMessages.CATEGORY_DEFAULT_DIRECTION_INVALID);
    }

    [Fact]
    public async Task BranchUserRequest_ShouldRetainFeatureMessageForUndefinedIntegerRole()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("InvalidRoleEnum", Role.Admin);
        var request = new
        {
            email = $"invalid-role-{Guid.NewGuid():N}@example.com",
            role = 999
        };

        using var response = await _client.PostAuthAsync("/branch/users", request, token);

        await AssertInvalidEnum(response, ResourcesErrorMessages.ROLE_INVALID);
    }

    [Fact]
    public async Task TimeEntryRequest_ShouldRetainFeatureMessageForUndefinedIntegerNullableAction()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("InvalidTimeEntryEnum", Role.Admin);
        var request = new
        {
            operatorId = Guid.NewGuid(),
            date = DateTime.Today,
            status = (int)TimeEntryStatus.Present,
            action = 999,
            segments = Array.Empty<object>()
        };

        using var response = await _client.PutAuthAsync("/timeentry", request, token);

        await AssertInvalidEnum(response, ResourcesErrorMessages.TIMEENTRY_STATUS_INVALID);
    }

    private static async Task AssertInvalidEnum(HttpResponseMessage response, string expectedMessage)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var error = await response.ReadContentAsync<TestResponseErrorJson>();
        error.ErrorMessages.ShouldBe([expectedMessage]);
    }
}
