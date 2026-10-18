namespace server.Domain.Interfaces;

/// <summary>No protocol/body logging; true means SMTP accepted, not inbox delivery.</summary>
public interface ISecurityMailSender
{
    Task<bool> Send(string recipient, string subject, string body, CancellationToken ct = default);
}

/// <summary>Bounded, volatile queue: capability plaintext never persists to application storage.</summary>
public interface IRecoveryMailQueue
{
    bool TryEnqueue(string recipient, string body);
}
