using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Platform;

[Collection(ServerApiCollection.Name)]
public sealed class FirstPlatformOperatorCliTest
{
    [Fact]
    public async Task EmptyMigratedDatabase_CanBootstrapThroughCapturedMail_WithoutPasswordArgument()
    {
        await using var factory = new ServerWebApplicationFactory();
        await factory.InitializeAsync();
        await using var mail = new LocalMailpitCapture();
        await mail.StartAsync();
        var email = $"first-{Guid.NewGuid():N}@example.invalid";
        (await factory.ReadDb(db => db.Users.CountAsync())).ShouldBe(0);

        var invalid = await Run(factory.DisposableConnectionString, mail.GetMappedPublicPort(1025), "invalid-email", false);
        invalid.Code.ShouldBe(1);
        var preview = await Run(factory.DisposableConnectionString, mail.GetMappedPublicPort(1025), email, false);
        preview.Code.ShouldBe(0, preview.Error);
        (await factory.ReadDb(db => db.Users.CountAsync())).ShouldBe(0);

        var failedDelivery = await Run(factory.DisposableConnectionString, mail.GetMappedPublicPort(1025),
            email, true, mailEnabled: false);
        failedDelivery.Code.ShouldBe(1);
        failedDelivery.Error.ShouldContain("não foi entregue");
        var created = await Run(factory.DisposableConnectionString, mail.GetMappedPublicPort(1025), email, true);
        created.Code.ShouldBe(0, created.Error);
        created.Output.ShouldContain("ativação=enviada");
        var token = await mail.WaitToken(email);
        var user = await factory.ReadDb(db => db.Users.SingleAsync());
        user.IsPlatformOperator.ShouldBeTrue();
        user.Password.ShouldBeEmpty();
        var reset = await factory.ReadDb(db => db.PasswordResetTokens.SingleAsync(x => x.Active));
        reset.TokenHash.ShouldNotBe(token);
        reset.ExpiresAt.ShouldBe(reset.CreatedAt.AddMinutes(7));

        using var client = factory.CreateClient();
        using var beforeActivation = await client.PostAsJsonAsync("/user/login", new RequestUserLoginJson
        { Email = email, Password = "SomePassword123" });
        beforeActivation.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var activated = await client.PostAsJsonAsync("/user/reset-password", new RequestResetPasswordJson
        { Token = token, NewPassword = "ActivatedPassword123" });
        activated.StatusCode.ShouldBe(HttpStatusCode.NoContent, await activated.Content.ReadAsStringAsync());
        using var login = await client.PostAsJsonAsync("/user/login", new RequestUserLoginJson
        { Email = email, Password = "ActivatedPassword123" });
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        var identity = (await login.Content.ReadFromJsonAsync<ResponseUserLoginJson>())!;
        identity.UserId.ShouldBe(user.Id);
        using var onboarding = await client.PostAuthAsync("/platform/onboarding", new RequestOperateBranchJson
        { RequestId = Guid.NewGuid(), Name = "Lotérica após ativação",
            OwnerEmail = $"owner-{Guid.NewGuid():N}@example.invalid" }, identity.ResponseToken.Token);
        onboarding.StatusCode.ShouldBe(HttpStatusCode.Created, await onboarding.Content.ReadAsStringAsync());
        (await factory.ReadDb(db => db.OperatedOnboardings.CountAsync())).ShouldBe(1);

        var second = await Run(factory.DisposableConnectionString, mail.GetMappedPublicPort(1025),
            $"other-{Guid.NewGuid():N}@example.invalid", true);
        second.Code.ShouldBe(1);
        (await factory.ReadDb(db => db.Users.CountAsync())).ShouldBe(1);
    }

    private static async Task<(int Code, string Output, string Error)> Run(string connection, int smtpPort,
        string email, bool execute, bool mailEnabled = true)
    {
        var cli = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "built-cli-path.txt")).Trim();
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in new[] { cli, "bootstrap-platform", "--environment", "Development",
                     "--connection", connection, "--name", "Operador inicial", "--email", email })
            start.ArgumentList.Add(argument);
        if (execute) start.ArgumentList.Add("--execute");
        start.Environment["LOTO_CLI_SecurityMail__Enabled"] = mailEnabled.ToString().ToLowerInvariant();
        start.Environment["LOTO_CLI_SecurityMail__LocalCapture"] = "true";
        start.Environment["LOTO_CLI_SecurityMail__Host"] = "127.0.0.1";
        start.Environment["LOTO_CLI_SecurityMail__Port"] = smtpPort.ToString();
        start.Environment["LOTO_CLI_AccountSecurity__ApplicationUrl"] = "http://127.0.0.1:3000/";
        start.Environment["LOTO_CLI_AccountSecurity__ResetMinutes"] = "7";
        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await process.StandardOutput.ReadToEndAsync(), await process.StandardError.ReadToEndAsync());
    }
}
