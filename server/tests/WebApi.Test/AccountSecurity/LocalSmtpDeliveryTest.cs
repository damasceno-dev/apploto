using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using server.Infrastructure.Services;
using Shouldly;
using Xunit;

namespace WebApi.Test.AccountSecurity;

/// <summary>Real pinned SMTP adapter and disposable Mailpit; no unit mail substitute or external recipient.</summary>
public sealed class LocalSmtpDeliveryTest : IAsyncLifetime
{
    private readonly WebApi.Test.Infrastructure.LocalMailpitCapture _mailpit = new();

    public Task InitializeAsync() => _mailpit.StartAsync();
    public Task DisposeAsync() => _mailpit.DisposeAsync().AsTask();

    [Fact]
    public async Task SmtpAdapter_ShouldSendRealMimeMessageToIsolatedLoopbackCapture()
    {
        var recipient = $"smtp-{Guid.NewGuid():N}@example.invalid";
        var sender = new SmtpSecurityMailSender(new SecurityMailOptions(true, "127.0.0.1",
            _mailpit.GetMappedPublicPort(1025), "no-reply@loto.invalid", true, null, null));
        (await sender.Send(recipient, "Redefinir senha", "Link de teste local: https://app.example.invalid/reset#token=local-test-only")).ShouldBeTrue();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{_mailpit.GetMappedPublicPort(8025)}") };
        using var messages = JsonDocument.Parse(await http.GetStringAsync("/api/v1/messages"));
        var message = messages.RootElement.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("To").EnumerateArray().Any(to => to.GetProperty("Address").GetString() == recipient));
        message.GetProperty("Subject").GetString().ShouldBe("Redefinir senha");
        var id = message.GetProperty("ID").GetString();
        using var details = JsonDocument.Parse(await http.GetStringAsync("/api/v1/message/" + id));
        details.RootElement.GetProperty("Text").GetString()!.ShouldContain("#token=local-test-only");
    }

    [Fact]
    public async Task RemoteMode_ShouldRequireStartTls_AndDisabledOrUnreachableNeverClaimsSend()
    {
        var port = _mailpit.GetMappedPublicPort(1025);
        var requireTls = new SmtpSecurityMailSender(new SecurityMailOptions(true, "127.0.0.1", port,
            "no-reply@loto.invalid", false, null, null));
        (await requireTls.Send("test@example.invalid", "Teste", "Sem TLS não deve enviar")).ShouldBeFalse();
        var disabled = new SmtpSecurityMailSender(new SecurityMailOptions(false, "127.0.0.1", port,
            "no-reply@loto.invalid", true, null, null));
        (await disabled.Send("test@example.invalid", "Teste", "Não enviado")).ShouldBeFalse();
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var closedPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var unavailable = new SmtpSecurityMailSender(new SecurityMailOptions(true, "127.0.0.1", closedPort,
            "no-reply@loto.invalid", true, null, null));
        (await unavailable.Send("test@example.invalid", "Teste", "Não enviado")).ShouldBeFalse();
    }

    [Theory]
    [InlineData("smtp.example.invalid")]
    [InlineData("localhost")]
    [InlineData("0.0.0.0")]
    public void PlaintextCapture_ShouldRejectNonLiteralLoopback(string host)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["SecurityMail:Enabled"] = "true", ["SecurityMail:LocalCapture"] = "true", ["SecurityMail:Host"] = host }).Build();
        Should.Throw<InvalidOperationException>(() => SecurityMailOptions.FromConfiguration(configuration));
    }
}
