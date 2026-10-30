using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.Services.Sessions;

/// <summary>
/// TotalOpening input of a session (§6.20 *Session opening in the §6.12 formula*): the immutable acknowledged vector
/// when one exists, otherwise the inherited closing of the counted predecessor.
/// </summary>
public sealed record ResolvedSessionOpening(
    IReadOnlyDictionary<Guid, decimal> Values,
    SessionOpening? Acknowledged,
    DailyClose? InheritedFrom)
{
    public bool IsAcknowledged => Acknowledged is not null;

    public decimal Total(Guid cashVarianceProductId) =>
        Values.Where(pair => pair.Key != cashVarianceProductId).Sum(pair => pair.Value);
}

/// <summary>
/// The proposal a new session must acknowledge. <see cref="SourceKind"/> is null when no source exists: absence is
/// never a zero opening.
/// </summary>
public sealed record SessionOpeningProposal(
    SessionOpeningSourceKind? SourceKind,
    Guid? SourceId,
    long? SourceRevision,
    DateTime? SourceDate,
    IReadOnlyDictionary<Guid, decimal> Values)
{
    public bool IsMissing => SourceKind is null;

    public static SessionOpeningProposal Missing { get; } = new(null, null, null, null, new Dictionary<Guid, decimal>());
}
