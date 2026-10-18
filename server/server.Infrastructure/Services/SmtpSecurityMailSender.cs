using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using server.Domain.Interfaces;

namespace server.Infrastructure.Services;

internal sealed class SmtpSecurityMailSender(SecurityMailOptions options) : ISecurityMailSender
{
    public async Task<bool> Send(string recipient, string subject, string body, CancellationToken ct = default)
    {
        if (!options.Enabled) return false;
        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(options.From));
            message.To.Add(MailboxAddress.Parse(recipient));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };
            using var client = new SmtpClient { Timeout = 5000 };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            await client.ConnectAsync(options.Host, options.Port,
                options.LocalCapture ? SecureSocketOptions.None : SecureSocketOptions.StartTls, timeout.Token);
            if (!string.IsNullOrEmpty(options.Username))
                await client.AuthenticateAsync(options.Username, options.Password ?? string.Empty, timeout.Token);
            await client.SendAsync(message, timeout.Token);
            // SMTP acceptance already occurred. A disconnect failure must not mislabel an accepted send.
            try { await client.DisconnectAsync(true, timeout.Token); } catch { /* no protocol/recipient/body logging */ }
            return true;
        }
        catch (Exception) { return false; } // Caller records bounded outcome; provider errors may contain secrets.
    }
}
