namespace server.Domain.Entities.Enums;

/// <summary>Why a count attempt exists, so a retry, a re-confirmation and a recount stay distinguishable.</summary>
public enum SessionCountAttemptKind
{
    /// <summary>The session's first Conferir.</summary>
    Initial = 0,

    /// <summary>The count was edited after the previous attempt.</summary>
    Correction = 1,

    /// <summary>An explicit Recontar preceded it (values may be identical).</summary>
    Recount = 2,

    /// <summary>The count is unchanged but the ledger or inherited opening changed; the operator confirmed it still describes the cashbox.</summary>
    StillObserved = 3
}
