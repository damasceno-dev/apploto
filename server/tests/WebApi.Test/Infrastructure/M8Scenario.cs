using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Application.Services.Security;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using server.Infrastructure.Services;
using Shouldly;

namespace WebApi.Test.Infrastructure;

internal static class M8Scenario
{
    extension(ServerWebApplicationFactory factory)
    {
        public SecurityMailCapture SecurityMail => factory.Services.GetRequiredService<SecurityMailCapture>();

        public async Task<ResponseUserLoginJson> Login(HttpClient client, User user, string password = TestSeeder.DefaultPassword)
        {
            using var response = await client.PostAsJsonAsync("/user/login", new RequestUserLoginJson { Email = user.Email, Password = password });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var result = await response.ReadContentAsync<ResponseUserLoginJson>();
            result.UserId.ShouldBe(user.Id);
            return result;
        }

        public async Task<string> RequestRecovery(HttpClient client, User user, int index = 0)
        {
            using var response = await client.PostAsJsonAsync("/user/forgot-password", new RequestForgotPasswordJson { Email = user.Email });
            response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
            (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
            return await factory.SecurityMail.WaitToken(user.Email, index);
        }

        public async Task<(ResponseInvitationJson Invitation, string Capability)> Invite(HttpClient client, string actorToken,
            string email, Role role = Role.Member, Guid? operatorId = null)
        {
            using var response = await client.PostAuthAsync("/invitation", new RequestIssueInvitationJson
            { Email = email, Role = role, OperatorId = operatorId }, actorToken);
            response.StatusCode.ShouldBe(HttpStatusCode.Created);
            var invitation = await response.ReadContentAsync<ResponseInvitationJson>();
            invitation.DeliveryStatus.ShouldBe(MailDeliveryStatus.Sent);
            return (invitation, await factory.SecurityMail.WaitToken(email));
        }

        public async Task<T> ReadDb<T>(Func<ServerDbContext, Task<T>> read)
        {
            using var scope = factory.Services.CreateScope();
            return await read(scope.ServiceProvider.GetRequiredService<ServerDbContext>());
        }

        public async Task UpdateDb(Func<ServerDbContext, Task> update)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            await update(db);
            await db.SaveChangesAsync();
        }

        public async Task<HeldAdvisoryLock> HoldSecurity(params string[] resources)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            return await HeldAdvisoryLock.AcquireAsync(db.Database.GetConnectionString()!, resources.Select(SecurityCoordination.Key).Min());
        }
    }
}
