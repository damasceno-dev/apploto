using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using server.Domain.Interfaces;

namespace server.Infrastructure.Services;

internal sealed class RecoveryMailQueue(ISecurityMailSender sender, ILogger<RecoveryMailQueue> logger)
    : BackgroundService, IRecoveryMailQueue
{
    private readonly Channel<(string Recipient, string Body)> _channel = Channel.CreateBounded<(string, string)>(
        new BoundedChannelOptions(100) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public bool TryEnqueue(string recipient, string body)
    {
        var accepted = _channel.Writer.TryWrite((recipient, body));
        if (!accepted) logger.LogWarning("Recovery mail queue full; a new request is required after cooldown.");
        return accepted;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                var sent = await sender.Send(message.Recipient, "Redefinir senha", message.Body, stoppingToken);
                if (!sent) logger.LogWarning("Recovery SMTP attempt failed; a new request is required after cooldown.");
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
