using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Clients;

/// <summary>
/// Happy-path coverage Client endpoints:
/// <c>POST /client</c>, <c>GET /client</c>, <c>GET /client/{id}</c>,
/// <c>PUT /client/{id}</c>, and <c>DELETE /client/{id}</c>.
///
/// Permission model under test:
/// - Create / List / Get / Update: any branch role (<see cref="Role.Member"/>,
///   <see cref="Role.Manager"/>, <see cref="Role.Admin"/>).
/// - Deactivate: <see cref="Role.Manager"/> or <see cref="Role.Admin"/> only.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class ClientControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    // -------------------------------------------------------------------------
    // POST /client
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Create_ShouldReturn201WithClient_WhenAdminCreatesClient()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliCreateAdmin");
        var request = new RequestCreateClientJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/client", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateClientJson>();
        payload.Id.ShouldNotBe(Guid.Empty);
        payload.Name.ShouldBe(request.Name);
        payload.Phone.ShouldBe(request.Phone);
        payload.BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Client>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.BranchId.ShouldBe(branch.Id);
        persisted.Name.ShouldBe(request.Name);
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenMemberCreatesClient()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliCreateMember", Role.Member);
        var request = new RequestCreateClientJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/client", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateClientJson>();
        payload.BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Client>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Create_ShouldReturn201_WhenManagerCreatesClient()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliCreateManager", Role.Manager);
        var request = new RequestCreateClientJsonBuilder().Build();

        var httpResponse = await _client.PostAuthAsync("/client", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateClientJson>();
        payload.BranchId.ShouldBe(branch.Id);
    }

    [Fact]
    public async Task Create_ShouldNormalizeCpf_WhenCpfIsFormattedWithPunctuaction()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CliCreateCpf");
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf("123.456.789-09")
            .Build();

        var httpResponse = await _client.PostAuthAsync("/client", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateClientJson>();
        payload.Cpf.ShouldBe("12345678909");

        var persisted = await factory.ReloadAsync<Client>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.Cpf.ShouldBe("12345678909");
    }

    [Fact]
    public async Task Create_ShouldPersistNullCpf_WhenCpfIsNotProvided()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CliCreateNoCpf");
        var request = new RequestCreateClientJsonBuilder()
            .WithCpf(null)
            .Build();

        var httpResponse = await _client.PostAuthAsync("/client", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var payload = await httpResponse.ReadContentAsync<ResponseCreateClientJson>();
        payload.Cpf.ShouldBeNull();

        var persisted = await factory.ReloadAsync<Client>(payload.Id);
        persisted.ShouldNotBeNull();
        persisted.Cpf.ShouldBeNull();
    }

    // -------------------------------------------------------------------------
    // GET /client
    // -------------------------------------------------------------------------

    [Fact]
    public async Task List_ShouldReturn200WithSeededClients_WhenAdminListsClients()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliList");
        var c1 = await factory.SeedClientAsync(branch.Id, "Alice");
        var c2 = await factory.SeedClientAsync(branch.Id, "Bob");

        var httpResponse = await _client.GetAuthAsync("/client", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListClientsJson>();
        payload.Clients.ShouldContain(c => c.Id == c1.Id && c.Name == c1.Name);
        payload.Clients.ShouldContain(c => c.Id == c2.Id && c.Name == c2.Name);
    }

    [Fact]
    public async Task List_ShouldReturn200WithEmptyList_WhenBranchHasNoClients()
    {
        var (_, _, _, token) = await factory.SeedFullBranchContextAsync("CliListEmpty");

        var httpResponse = await _client.GetAuthAsync("/client", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListClientsJson>();
        payload.Clients.ShouldNotBeNull();
    }

    [Fact]
    public async Task List_ShouldReturn200_WhenMemberListsClients()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliListMember", Role.Member);
        await factory.SeedClientAsync(branch.Id, "MemberVisible");

        var httpResponse = await _client.GetAuthAsync("/client", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseListClientsJson>();
        payload.Clients.ShouldContain(c => c.Name == "MemberVisible");
    }

    // -------------------------------------------------------------------------
    // GET /client/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Get_ShouldReturn200WithClient_WhenAdminGetsExistingClient()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliGet");
        var seededClient = await factory.SeedClientAsync(branch.Id, "Get Test Client");

        var httpResponse = await _client.GetAuthAsync($"/client/{seededClient.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseClientJson>();
        payload.Id.ShouldBe(seededClient.Id);
        payload.Name.ShouldBe(seededClient.Name);
        payload.BranchId.ShouldBe(branch.Id);
    }

    [Fact]
    public async Task Get_ShouldReturn200_WhenMemberGetsClient()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliGetMember", Role.Member);
        var seededClient = await factory.SeedClientAsync(branch.Id, "Member Get Client");

        var httpResponse = await _client.GetAuthAsync($"/client/{seededClient.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseClientJson>();
        payload.Id.ShouldBe(seededClient.Id);
    }

    // -------------------------------------------------------------------------
    // PUT /client/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Update_ShouldReturn200AndPersistNewData_WhenAdminUpdatesClient()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliUpdate");
        var seededClient = await factory.SeedClientAsync(branch.Id, "Old Name");
        var request = new RequestUpdateClientJsonBuilder()
            .WithName("New Name")
            .WithCpf(null)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/client/{seededClient.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseClientJson>();
        payload.Id.ShouldBe(seededClient.Id);
        payload.Name.ShouldBe("New Name");
        payload.BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Client>(seededClient.Id);
        persisted.ShouldNotBeNull();
        persisted.Name.ShouldBe("New Name");
        persisted.Active.ShouldBeTrue();
    }

    [Fact]
    public async Task Update_ShouldReturn200_WhenMemberUpdatesClient()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliUpdateMember", Role.Member);
        var seededClient = await factory.SeedClientAsync(branch.Id, "Before");
        var request = new RequestUpdateClientJsonBuilder()
            .WithName("After")
            .WithCpf(null)
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/client/{seededClient.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseClientJson>();
        payload.Name.ShouldBe("After");
    }

    [Fact]
    public async Task Update_ShouldNormalizeCpf_WhenCpfIsFormattedWithPunctuation()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliUpdateCpf");
        var seededClient = await factory.SeedClientAsync(branch.Id);
        var request = new RequestUpdateClientJsonBuilder()
            .WithCpf("987.654.321-00")
            .Build();

        var httpResponse = await _client.PutAuthAsync($"/client/{seededClient.Id}", request, token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseClientJson>();
        payload.Cpf.ShouldBe("98765432100");

        var persisted = await factory.ReloadAsync<Client>(seededClient.Id);
        persisted.ShouldNotBeNull();
        persisted.Cpf.ShouldBe("98765432100");
    }

    // -------------------------------------------------------------------------
    // DELETE /client/{id}
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Deactivate_ShouldReturn200AndSoftDelete_WhenAdminDeactivatesClient()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliDeactivateAdmin");
        var seededClient = await factory.SeedClientAsync(branch.Id, "To Be Deactivated");

        var httpResponse = await _client.DeleteAuthAsync($"/client/{seededClient.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseClientJson>();
        payload.Id.ShouldBe(seededClient.Id);
        payload.Name.ShouldBe(seededClient.Name);
        payload.BranchId.ShouldBe(branch.Id);

        var persisted = await factory.ReloadAsync<Client>(seededClient.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
    }

    [Fact]
    public async Task Deactivate_ShouldReturn200AndSoftDelete_WhenManagerDeactivatesClient()
    {
        var (_, branch, _, token) = await factory.SeedFullBranchContextAsync("CliDeactivateManager", Role.Manager);
        var seededClient = await factory.SeedClientAsync(branch.Id, "Manager Deactivate");

        var httpResponse = await _client.DeleteAuthAsync($"/client/{seededClient.Id}", token);

        httpResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var payload = await httpResponse.ReadContentAsync<ResponseClientJson>();
        payload.Id.ShouldBe(seededClient.Id);

        var persisted = await factory.ReloadAsync<Client>(seededClient.Id);
        persisted.ShouldNotBeNull();
        persisted.Active.ShouldBeFalse();
    }
}
