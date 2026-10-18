using System.Collections.Concurrent;
using server.Domain.Interfaces;

namespace WebApi.Test.Infrastructure;

/// <summary>Isolated fake sender for HTTP tests. Actual SMTP is tested separately over loopback.</summary>
internal sealed class SecurityMailCapture : ISecurityMailSender
{
    public readonly ConcurrentQueue<(string Recipient, string Body)> Messages = new();
    public readonly ConcurrentDictionary<string, bool> FailRecipients = new();

    public Task<bool> Send(string recipient, string subject, string body, CancellationToken ct = default)
    {
        if (FailRecipients.ContainsKey(recipient)) return Task.FromResult(false);
        Messages.Enqueue((recipient, body));
        return Task.FromResult(true);
    }

    public async Task<string> WaitToken(string email, int index = 0)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var messages = Messages.Where(m => m.Recipient == email).ToArray();
            if (messages.Length > index)
                return Uri.UnescapeDataString(messages[index].Body.Split("#token=", StringSplitOptions.None)[1]);
            await Task.Delay(20);
        }
        throw new TimeoutException("Expected isolated capture message did not arrive.");
    }
}
