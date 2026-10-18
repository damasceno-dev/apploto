using System.Net;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace server.Infrastructure.Services;

internal sealed record SecurityMailOptions(bool Enabled, string Host, int Port, string From, bool LocalCapture, string? Username, string? Password)
{
    internal static SecurityMailOptions FromConfiguration(IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("SecurityMail:Enabled");
        var host = configuration["SecurityMail:Host"] ?? "127.0.0.1";
        var port = configuration.GetValue<int?>("SecurityMail:Port") ?? 1025;
        var from = configuration["SecurityMail:From"] ?? "no-reply@loto.invalid";
        var capture = configuration.GetValue<bool>("SecurityMail:LocalCapture");
        if (port is < 1 or > 65535 || string.IsNullOrWhiteSpace(host) || !MailboxAddress.TryParse(from, out _))
            throw new InvalidOperationException("Invalid SecurityMail host, port or sender.");
        // An explicit literal loopback address avoids DNS rebind/downgrade in plaintext capture mode.
        if (capture && (!IPAddress.TryParse(host, out var ip) || !IPAddress.IsLoopback(ip)))
            throw new InvalidOperationException("SecurityMail:LocalCapture requires a literal loopback host.");
        return new(enabled, host, port, from, capture, configuration["SecurityMail:Username"], configuration["SecurityMail:Password"]);
    }
}
