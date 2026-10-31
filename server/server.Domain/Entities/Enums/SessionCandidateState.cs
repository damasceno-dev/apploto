namespace server.Domain.Entities.Enums;

/// <summary>Server-owned state of a session's confirmed count candidate (§6.20/§6.21, M9.6 Phase 4).</summary>
public enum SessionCandidateState
{
    /// <summary>No Conferir yet: expected closing and difference stay hidden.</summary>
    NotConferred = 0,

    /// <summary>The latest attempt matches the current count, ledger and opening revisions and token.</summary>
    Current = 1,

    /// <summary>The count was edited after the latest attempt; its result no longer applies and nothing new is revealed.</summary>
    Outdated = 2,

    /// <summary>The count is unchanged but the ledger or inherited opening changed; a separately labelled recalculation is shown.</summary>
    Unconfirmed = 3,

    /// <summary>Recontar started after the latest attempt; the calculation stays hidden until the next Conferir.</summary>
    Recounting = 4
}
