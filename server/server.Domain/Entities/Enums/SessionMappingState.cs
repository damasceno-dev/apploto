namespace server.Domain.Entities.Enums;

/// <summary>
/// Whether a session's custody identity is known. <see cref="Unresolved"/> rows are legacy history kept read-only
/// until a reviewed mapping resolves them; operational constraints and commands apply only to <see cref="Ready"/>.
/// </summary>
public enum SessionMappingState
{
    Ready = 0,
    Unresolved = 1
}
