using System.Net;
using System.Net.Http.Json;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Platform;

[Collection(ServerApiCollection.Name)]
public sealed class OperatedOnboardingHttpTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task PlatformOnboarding_ShouldSeedAtomically_InviteOwner_AndNeverGrantCallerTenantAccess()
    {
        var platform = await factory.SeedUserAsync();
        await SetPlatform(platform.Id, true);
        var token = factory.IssueGlobalToken(platform);
        var email = $"owner-{Guid.NewGuid():N}@example.invalid";
        var request = Sample(email);
        using var created = await client.PostAuthAsync("/platform/onboarding", request, token);
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var response = await created.ReadContentAsync<ResponseOperatedOnboardingJson>();
        response.RequestId.ShouldBe(request.RequestId);
        response.OwnerEmail.ShouldBe(email);
        response.OwnerInvitationStatus.ShouldBe(InvitationStatus.Pending);
        response.DeliveryStatus.ShouldBe(MailDeliveryStatus.Sent);
        var capability = await factory.SecurityMail.WaitToken(email);

        using var repeat = await client.PostAuthAsync("/platform/onboarding", request, token);
        repeat.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await repeat.ReadContentAsync<ResponseOperatedOnboardingJson>()).BranchId.ShouldBe(response.BranchId);
        using var overposted = await client.PostAuthAsync("/platform/onboarding", new
        {
            request.RequestId, request.Name, request.OwnerEmail, request.Terminals,
            request.AdditionalProducts, request.Operators,
            platformUserId = platform.Id, ownerRole = Role.Member, branchUserId = platform.Id,
            isPlatformOperator = true
        }, token);
        overposted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await overposted.ReadContentAsync<ResponseOperatedOnboardingJson>()).BranchId.ShouldBe(response.BranchId);
        using var conflict = await client.PostAuthAsync("/platform/onboarding", Sample(email, request.RequestId, "Outra"), token);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await factory.ReadDb(db => db.OperatedOnboardings.CountAsync(x => x.RequestId == request.RequestId))).ShouldBe(1);
        (await factory.ReadDb(db => db.BranchUsers.CountAsync(x => x.BranchId == response.BranchId))).ShouldBe(0);
        (await factory.ReadDb(db => db.Products.CountAsync(x => x.BranchId == response.BranchId))).ShouldBe(9);
        (await factory.ReadDb(db => db.Accounts.CountAsync(x => x.BranchId == response.BranchId))).ShouldBe(2);
        (await factory.ReadDb(db => db.Operators.CountAsync(x => x.BranchId == response.BranchId))).ShouldBe(1);
        (await factory.ReadDb(db => db.OperatorAccounts.CountAsync(x => x.Operator.BranchId == response.BranchId))).ShouldBe(2);
        using var noBranches = await client.GetAuthAsync("/branch/my-branches", token);
        (await noBranches.ReadContentAsync<ResponseListMyBranchesJson>()).Branches.ShouldBeEmpty();
        using var noSession = await client.PostAuthAsync("/branch/session", new RequestCreateBranchSessionJson { BranchId = response.BranchId }, token);
        noSession.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        using var accepted = await client.PostAsJsonAsync("/invitation/register", new RequestRegisterInvitationJson
        { Token = capability, Name = "Dono", Password = TestSeeder.DefaultPassword });
        accepted.StatusCode.ShouldBe(HttpStatusCode.Created);
        var owner = await accepted.ReadContentAsync<ResponseUserLoginJson>();
        using var ownerSession = await client.PostAuthAsync("/branch/session", new RequestCreateBranchSessionJson { BranchId = response.BranchId }, owner.ResponseToken.Token);
        ownerSession.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await factory.ReadDb(db => db.BranchUsers.SingleAsync(x => x.BranchId == response.BranchId))).Role.ShouldBe(Role.Admin);
    }

    [Fact]
    public async Task PlatformGuard_ShouldRejectBranchAdmin_UnflaggedIdentity_AndRemovedFlag()
    {
        var (admin, _, _, branchToken) = await factory.SeedFullBranchContextAsync("PlatformDenied");
        var request = Sample($"denied-{Guid.NewGuid():N}@example.invalid");
        using var identityDenied = await client.PostAuthAsync("/platform/onboarding", request, factory.IssueGlobalToken(admin));
        identityDenied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        using var branchDenied = await client.PostAuthAsync("/platform/onboarding", request, branchToken);
        branchDenied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await SetPlatform(admin.Id, true);
        var token = factory.IssueGlobalToken(admin);
        await SetPlatform(admin.Id, false);
        using var revoked = await client.PostAuthAsync("/platform/onboarding", request, token);
        revoked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await factory.ReadDb(db => db.OperatedOnboardings.CountAsync(x => x.RequestId == request.RequestId))).ShouldBe(0);
    }

    [Fact]
    public async Task ExpiredOwnerInvite_ShouldReissueWithoutCloningBranch_AndInvalidateOldCapability()
    {
        var platform = await factory.SeedUserAsync();
        await SetPlatform(platform.Id, true);
        var token = factory.IssueGlobalToken(platform);
        var email = $"reissue-{Guid.NewGuid():N}@example.invalid";
        var request = Sample(email);
        using var created = await client.PostAuthAsync("/platform/onboarding", request, token);
        var response = await created.ReadContentAsync<ResponseOperatedOnboardingJson>();
        var oldToken = await factory.SecurityMail.WaitToken(email);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            (await db.Invitations.FindAsync(response.OwnerInvitationId))!.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await db.SaveChangesAsync();
        }
        using var reissued = await client.PostAuthAsync($"/platform/onboarding/{request.RequestId}/reissue-owner-invitation", token);
        reissued.StatusCode.ShouldBe(HttpStatusCode.OK);
        var newToken = await factory.SecurityMail.WaitToken(email, 1);
        newToken.ShouldNotBe(oldToken);
        using var replay = await client.PostAsJsonAsync("/invitation/register", new RequestRegisterInvitationJson
        { Token = oldToken, Name = "Dono", Password = TestSeeder.DefaultPassword });
        replay.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        using var accepted = await client.PostAsJsonAsync("/invitation/register", new RequestRegisterInvitationJson
        { Token = newToken, Name = "Dono", Password = TestSeeder.DefaultPassword });
        accepted.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await factory.ReadDb(db => db.OperatedOnboardings.CountAsync(x => x.RequestId == request.RequestId))).ShouldBe(1);
    }

    [Fact]
    public async Task CliOnboard_ShouldUseTheSameRequestIdentityAndBranchAsHttp()
    {
        var platform = await factory.SeedUserAsync();
        await SetPlatform(platform.Id, true);
        var request = Sample($"cli-{Guid.NewGuid():N}@example.invalid");
        var path = Path.Combine(Path.GetTempPath(), $"loto-cli-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
        try
        {
            var output = await RunCli(platform.Id, path, execute: true);
            output.ShouldContain($"RequestId={request.RequestId}");
            output.ShouldNotContain(factory.DisposableConnectionString);
            var record = await factory.ReadDb(db => db.OperatedOnboardings.SingleAsync(x => x.RequestId == request.RequestId));
            output.ShouldContain($"BranchId={record.BranchId}");
            using var repeated = await client.PostAuthAsync("/platform/onboarding", request, factory.IssueGlobalToken(platform));
            repeated.StatusCode.ShouldBe(HttpStatusCode.OK);
            (await repeated.ReadContentAsync<ResponseOperatedOnboardingJson>()).BranchId.ShouldBe(record.BranchId);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task CliDemoData_ShouldBeExplicitFakeAndDryRunShouldNotWrite()
    {
        var platform = await factory.SeedUserAsync();
        await SetPlatform(platform.Id, true);
        var request = new RequestOperateBranchJson
        {
            RequestId = Guid.NewGuid(), Name = "Demonstração descartável",
            OwnerEmail = $"demo-{Guid.NewGuid():N}@example.invalid"
        };
        var path = Path.Combine(Path.GetTempPath(), $"loto-cli-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
        try
        {
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new RequestOperateBranchJson
            { RequestId = Guid.NewGuid(), Name = " ", OwnerEmail = "invalid-email" }));
            (await RunCli(platform.Id, path, execute: false, expectSuccess: false))
                .ShouldContain(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(request));
            var denied = await factory.SeedUserAsync();
            (await RunCli(denied.Id, path, execute: false, demo: true, expectSuccess: false))
                .ShouldContain(ResourcesErrorMessages.PLATFORM_ONLY);
            (await RunCli(platform.Id, path, execute: false, demo: true)).ShouldContain("executar=False");
            (await factory.ReadDb(db => db.OperatedOnboardings.CountAsync(x => x.RequestId == request.RequestId))).ShouldBe(0);
            (await RunCli(platform.Id, path, execute: true, demo: true)).ShouldContain($"RequestId={request.RequestId}");
            var record = await factory.ReadDb(db => db.OperatedOnboardings.SingleAsync(x => x.RequestId == request.RequestId));
            (await factory.ReadDb(db => db.Accounts.CountAsync(x => x.BranchId == record.BranchId))).ShouldBe(2);
            (await factory.ReadDb(db => db.Operators.CountAsync(x => x.BranchId == record.BranchId))).ShouldBe(1);
            (await factory.ReadDb(db => db.Products.CountAsync(x => x.BranchId == record.BranchId))).ShouldBe(9);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task CliPlatformFlag_ShouldRequireExecute_AndRevokeAuthorityImmediately()
    {
        var user = await factory.SeedUserAsync();
        var request = Sample($"platform-cli-{Guid.NewGuid():N}@example.invalid");
        (await RunPlatformFlagCli(user.Id, true, execute: false)).ShouldContain("executar=False");
        (await factory.ReadDb(db => db.Users.SingleAsync(x => x.Id == user.Id))).IsPlatformOperator.ShouldBeFalse();
        (await RunPlatformFlagCli(user.Id, true, execute: true)).ShouldContain("operador-plataforma=True");
        var token = factory.IssueGlobalToken(user);
        using var created = await client.PostAuthAsync("/platform/onboarding", request, token);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await RunPlatformFlagCli(user.Id, false, execute: true)).ShouldContain("operador-plataforma=False");
        using var denied = await client.PostAuthAsync("/platform/onboarding", Sample($"revoked-{Guid.NewGuid():N}@example.invalid"), token);
        denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ConcurrentRepeat_ShouldCreateExactlyOneBranchAndOwnerInvitation()
    {
        var platform = await factory.SeedUserAsync();
        await SetPlatform(platform.Id, true);
        var token = factory.IssueGlobalToken(platform);
        var request = Sample($"concurrent-{Guid.NewGuid():N}@example.invalid");
        var first = client.PostAuthAsync("/platform/onboarding", request, token);
        var second = client.PostAuthAsync("/platform/onboarding", request, token);
        using var a = await first;
        using var b = await second;
        new[] { a.StatusCode, b.StatusCode }.Order().ShouldBe([HttpStatusCode.OK, HttpStatusCode.Created]);
        var left = await a.ReadContentAsync<ResponseOperatedOnboardingJson>();
        var right = await b.ReadContentAsync<ResponseOperatedOnboardingJson>();
        left.BranchId.ShouldBe(right.BranchId);
        (await factory.ReadDb(db => db.OperatedOnboardings.CountAsync(x => x.RequestId == request.RequestId))).ShouldBe(1);
        (await factory.ReadDb(db => db.Invitations.CountAsync(x => x.BranchId == left.BranchId))).ShouldBe(1);
    }

    [Fact]
    public async Task InvitationInsertFailure_ShouldRollBackBranchSeedsAndRequestIdentity()
    {
        var platform = await factory.SeedUserAsync();
        await SetPlatform(platform.Id, true);
        var request = Sample($"rollback-{Guid.NewGuid():N}@example.invalid", name: "Rollback fixture " + Guid.NewGuid().ToString("N"));
        var token = factory.IssueGlobalToken(platform);
        const string createFunction = """
            CREATE FUNCTION m8_reject_invitation_fixture() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW."Email" LIKE 'rollback-%@example.invalid' THEN
                    RAISE EXCEPTION 'fixture rejects invitation';
                END IF;
                RETURN NEW;
            END $$;
            """;
        const string createTrigger = """
            CREATE TRIGGER m8_reject_invitation_fixture BEFORE INSERT ON "Invitations"
            FOR EACH ROW EXECUTE FUNCTION m8_reject_invitation_fixture();
            """;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            await db.Database.ExecuteSqlRawAsync(createFunction);
            await db.Database.ExecuteSqlRawAsync(createTrigger);
        }
        try
        {
            using var failed = await client.PostAuthAsync("/platform/onboarding", request, token);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            (await factory.ReadDb(db => db.OperatedOnboardings.CountAsync(x => x.RequestId == request.RequestId))).ShouldBe(0);
            (await factory.ReadDb(db => db.Branches.CountAsync(x => x.Name == request.Name))).ShouldBe(0);
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER m8_reject_invitation_fixture ON \"Invitations\"");
            await db.Database.ExecuteSqlRawAsync("DROP FUNCTION m8_reject_invitation_fixture()");
        }
        using var retry = await client.PostAuthAsync("/platform/onboarding", request, token);
        retry.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await factory.ReadDb(db => db.OperatedOnboardings.CountAsync(x => x.RequestId == request.RequestId))).ShouldBe(1);
    }

    private async Task SetPlatform(Guid id, bool enabled)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServerDbContext>();
        (await db.Users.FindAsync(id))!.IsPlatformOperator = enabled;
        await db.SaveChangesAsync();
    }

    private async Task<string> RunCli(Guid actorId, string requestFile, bool execute, bool demo = false, bool expectSuccess = true)
    {
        var cli = BuiltCliPath();
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { cli, "onboard", "--environment", "Development", "--connection",
                     factory.DisposableConnectionString, "--actor-user-id", actorId.ToString(),
                     "--request-file", requestFile }) start.ArgumentList.Add(argument);
        if (demo) start.ArgumentList.Add("--demo-data");
        if (execute) start.ArgumentList.Add("--execute");
        start.Environment["LOTO_CLI_SecurityMail__Enabled"] = "false";
        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        process.ExitCode.ShouldBe(expectSuccess ? 0 : 1, error);
        return expectSuccess ? output : error;
    }

    private async Task<string> RunPlatformFlagCli(Guid targetId, bool enabled, bool execute)
    {
        var cli = BuiltCliPath();
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { cli, "set-platform", "--environment", "Development", "--connection",
                     factory.DisposableConnectionString, "--target-user-id", targetId.ToString(),
                     "--enabled", enabled.ToString().ToLowerInvariant() }) start.ArgumentList.Add(argument);
        if (execute) start.ArgumentList.Add("--execute");
        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        process.ExitCode.ShouldBe(0, error);
        return output;
    }

    private static RequestOperateBranchJson Sample(string email, Guid? requestId = null, string name = "Lotérica teste") => new()
    {
        RequestId = requestId ?? Guid.NewGuid(), Name = name, OwnerEmail = email,
        Terminals = [new RequestOnboardingTerminalJson { Name = "Terminal 1", PairedTabName = "Fiado 1" }],
        AdditionalProducts = ["Produto local"],
        Operators = [new RequestOnboardingOperatorJson { Name = "Operador teste", TerminalIndexes = [0] }]
    };

    private static string BuiltCliPath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "built-cli-path.txt");
        File.Exists(path).ShouldBeTrue("WebApi.Test must build its server.Cli project reference");
        var cli = File.ReadAllText(path).Trim();
        File.Exists(cli).ShouldBeTrue("the referenced CLI binary must exist");
        return cli;
    }
}
