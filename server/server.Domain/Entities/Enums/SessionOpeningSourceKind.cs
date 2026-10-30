namespace server.Domain.Entities.Enums;

/// <summary>Where a proposed opening came from. A missing source is never <see cref="ExplicitZero"/>.</summary>
public enum SessionOpeningSourceKind
{
    ExplicitZero = 0,
    OpeningCount = 1,
    PreviousSession = 2,
    ImportedSession = 3
}
