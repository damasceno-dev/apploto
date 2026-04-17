using System.Net;
using CommonTestUtilities.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Clients;

/// <summary>
/// Unhappy-path coverage for the Milestone 2 / Phase 3 Client endpoints. Drives the
/// real HTTP pipeline to verify the stable error contract across authentication (401),
/// role-based authorization (403), missing or cross-branch resources (404), CPF
/// conflict (409), and validation (400).
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ClientControllerUnhappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // 401 — missing token on all client endpoints.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestCreateClientJsonBuilder().Build();

        var httpResponse = await _client.PostAsync("/client",
            System.Net.Http.Json.JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task List_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync("/client");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Get_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.GetAsync($"/client/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn401_WhenTokenIsMissing()
    {
        var request = new RequestUpdateClientJsonBuilder().Build();

        var httpResponse = await _client.PutAsync($"/client/{Guid.NewGuid()}",
            System.Net.Http.Json.JsonContent.Create(request));

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn401_WhenTokenIsMissing()
    {
        var httpResponse = await _client.DeleteAsync($"/client/{Guid.NewGuid()}");

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 403 — Member role is rejected by TokenAuthorize(Manager, Admin) on Deactivate.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Deactivate_ShouldReturn403_WhenCallerIsMember()
    {
        var (_, branch, _, memberToken) = await factory.SeedFullBranchContextAsync("CliDeactivateForbidden", Role.Member);
        var seededClient = await factory.SeedClientAsync(branch.Id);

        var httpResponse = await _client.DeleteAuthAsync($"/client/{seededClient.Id}", memberToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        // Client must remain active — the forbidden call must not mutate state.
        var persisted = await factory.ReloadAsync<Client>(seededClient.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // 400 — Shape validation failures surfaced through the HTTP pipeline.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn400_WhenNameIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CliCreateInvalidName");
        var request = new RequestCreateClientJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/client", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.NAME_EMPTY);
    }

    [Fact]
    public async Task Create_ShouldReturn400_WhenPhoneIsEmpty()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CliCreateInvalidPhone");
        var request = new RequestCreateClientJsonBuilder()
            .WithPhone(string.Empty)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/client", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_PHONE_EMPTY);
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenNameIsEmpty()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliUpdateInvalidName");
        var seededClient = await factory.SeedClientAsync(branch.Id);
        var request = new RequestUpdateClientJsonBuilder()
            .WithName(string.Empty)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/client/{seededClient.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Update_ShouldReturn400_WhenPhoneIsEmpty()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliUpdateInvalidPhone");
        var seededClient = await factory.SeedClientAsync(branch.Id);
        var request = new RequestUpdateClientJsonBuilder()
            .WithPhone(string.Empty)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/client/{seededClient.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_PHONE_EMPTY);
    }

    // -------------------------------------------------------------------------
    // 404 — Unknown or cross-branch client ids.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldReturn404_WhenClientDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CliGetMissing");

        var httpResponse = await _client.GetAuthAsync($"/client/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_NOT_FOUND);
    }

    [Fact]
    public async Task Get_ShouldReturn404_WhenClientBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("CliGetAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("CliGetVictim");
        var victimClient = await factory.SeedClientAsync(victimBranch.Id, "Victim Client");

        var httpResponse = await _client.GetAuthAsync($"/client/{victimClient.Id}", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_NOT_FOUND);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenClientDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CliUpdateMissing");
        var request = new RequestUpdateClientJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/client/{Guid.NewGuid()}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_NOT_FOUND);
    }

    [Fact]
    public async Task Update_ShouldReturn404_WhenClientBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("CliUpdateAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("CliUpdateVictim");
        var victimClient = await factory.SeedClientAsync(victimBranch.Id, "Victim Update Client");
        var request = new RequestUpdateClientJsonBuilder().Build();

        var httpResponse = await _client.PutAuthAsync($"/client/{victimClient.Id}", request, attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenClientDoesNotExist()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CliDeactivateMissing");

        var httpResponse = await _client.DeleteAuthAsync($"/client/{Guid.NewGuid()}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_NOT_FOUND);
    }

    [Fact]
    public async Task Deactivate_ShouldReturn404_WhenClientBelongsToDifferentBranch()
    {
        var (_, _, _, attackerToken) = await factory.SeedFullBranchContextAsync("CliDeactivateAttacker");
        var (_, victimBranch, _, _) = await factory.SeedFullBranchContextAsync("CliDeactivateVictim");
        var victimClient = await factory.SeedClientAsync(victimBranch.Id, "Victim Deactivate Client");

        var httpResponse = await _client.DeleteAuthAsync($"/client/{victimClient.Id}", attackerToken);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_NOT_FOUND);

        // Victim client must remain active — the cross-branch attempt must not mutate state.
        var persisted = await factory.ReloadAsync<Client>(victimClient.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // 409 — CPF uniqueness conflict (application-layer check and DB filtered index).
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn409_WhenCpfAlreadyExistsInBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliCreateCpfConflict");
        var cpf = "11122233344";
        await factory.SeedClientAsync(branch.Id, cpf: cpf);

        var request = new RequestCreateClientJsonBuilder()
            .WithCpf(cpf)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/client", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_CPF_CONFLICT);
    }

    [Fact]
    public async Task Create_ShouldReturn409_WhenFormattedCpfMatchesExistingNormalizedCpf()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliCreateCpfFormatConflict");
        var normalizedCpf = "55566677788";
        await factory.SeedClientAsync(branch.Id, cpf: normalizedCpf);

        // Submit the same CPF but with punctuation — use case normalizes before the check.
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf("555.666.777-88")
            .Build();

        var httpResponse = await _client.PostAuthAsync("/client", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_CPF_CONFLICT);
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenSameCpfExistsInDifferentBranch()
    {
        // The filtered unique index is scoped to (BranchId, Cpf). The same CPF is
        // valid in a different branch — this confirms the scope is honored end-to-end.
        var (_, branch1, _, token1) = await factory.SeedFullBranchContextAsync("CliCpfBranch1");
        var (_, branch2, _, _) = await factory.SeedFullBranchContextAsync("CliCpfBranch2");
        var sharedCpf = "99988877766";
        await factory.SeedClientAsync(branch2.Id, cpf: sharedCpf);

        var request = new RequestCreateClientJsonBuilder()
            .WithCpf(sharedCpf)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/client", request, token1);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Update_ShouldReturn409_WhenCpfAlreadyUsedByAnotherClientInBranch()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliUpdateCpfConflict");
        var existingCpf = "44455566677";
        await factory.SeedClientAsync(branch.Id, cpf: existingCpf);
        var targetClient = await factory.SeedClientAsync(branch.Id);

        var request = new RequestUpdateClientJsonBuilder()
            .WithCpf(existingCpf)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/client/{targetClient.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var payload = await httpResponse.ReadContentAsync<TestResponseErrorJson>();
        payload.ErrorMessages.ShouldContain(ResourcesErrorMessages.CLIENT_CPF_CONFLICT);
    }
}
