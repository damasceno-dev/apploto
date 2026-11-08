using System.Net;
using CommonTestUtilities.Requests;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
namespace WebApi.Test.People;

internal sealed record PeopleTestScenario(Guid BranchId, Guid UserId, Guid MembershipId, Operator Person, string Token)
{
    public static async Task<PeopleTestScenario> Seed(ServerWebApplicationFactory factory)
    {
        var (user, branch, member, token) = await factory.SeedFullBranchContextAsync("M98", Role.Manager);
        await factory.SeedSettingAsync(branch.Id, dailyTargetHours: 8, lunchDeductionOver6H: 0, lunchDeductionOver4H: 0);
        await factory.EnsureCashVarianceProductAsync(branch.Id);
        var person = await factory.SeedOperatorAsync(branch.Id, "Pessoa sintética com nome extenso para demonstrativo e histórico de múltiplos períodos");
        return new PeopleTestScenario(branch.Id, user.Id, member.Id, person, token);
    }
    public async Task<T> Create<T>(HttpClient client, string route, RequestPeopleEvidenceJson request, string? key = null) where T : class
    {
        var response = await client.PostAuthAsync("/people/" + route, (object)request, Token, key ?? Guid.NewGuid().ToString("N"));
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<T>();
    }
    // Generic helper uses compile-time payload shape; JsonContent serialization must include derived properties.
    public async Task<ResponseEmployeeStatementJson> Statement(HttpClient client, int month = 9, string extra = "")
    {
        var response = await client.GetAuthAsync($"/report/employee-statement?operatorId={Person.Id}&year=2026&month={month}" + extra, Token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseEmployeeStatementJson>();
    }
}
