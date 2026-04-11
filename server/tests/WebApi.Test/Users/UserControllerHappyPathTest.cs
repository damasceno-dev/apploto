using System.Net;
using System.Net.Http.Json;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Users;

[Collection(ServerApiCollection.Name)]
public class UserControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_ShouldReturnCreatedWithTokens()
    {
        var request = new RequestUserRegisterJsonBuilder()
            .WithEmail($"register-{Guid.NewGuid():N}@example.com")
            .Build();

        var httpResponse = await _client.PostAsJsonAsync("/user/register", request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseUserRegisterJson>();
        payload.Name.ShouldBe(request.Name);
        payload.Email.ShouldBe(request.Email);
        payload.ResponseToken.ShouldNotBeNull();
        payload.ResponseToken.Token.ShouldNotBeNullOrWhiteSpace();
        payload.ResponseToken.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_ShouldReturnOkWithTokens_WhenCredentialsMatchASeededUser()
    {
        var user = await factory.SeedUserAsync();
        var request = new RequestUserLoginJsonBuilder()
            .WithEmail(user.Email)
            .WithPassword(TestSeeder.DefaultPassword)
            .Build();

        var httpResponse = await _client.PostAsJsonAsync("/user/login", request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseUserLoginJson>();
        payload.Email.ShouldBe(user.Email);
        payload.Name.ShouldBe(user.Name);
        payload.ResponseToken.Token.ShouldNotBeNullOrWhiteSpace();
        payload.ResponseToken.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task RenewToken_ShouldReturnOkWithFreshTokens_WhenRefreshTokenIsValid()
    {
        var user = await factory.SeedUserAsync();
        var refreshToken = await factory.SeedRefreshTokenAsync(user.Id);
        var request = new RequestUserRenewTokenJsonBuilder()
            .WithValue(refreshToken.Value)
            .Build();

        var httpResponse = await _client.PostAsJsonAsync("/user/renew-token", request);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseTokenJson>();
        payload.Token.ShouldNotBeNullOrWhiteSpace();
        payload.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        payload.RefreshToken.ShouldNotBe(refreshToken.Value);
    }
}
