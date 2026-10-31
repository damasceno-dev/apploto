namespace server.Communication.Requests;

/// <summary>Conferir (§6.20 ConferSessionCount): the server-issued token and the revisions the caller read with it.</summary>
public class RequestConferSessionCountJson
{
    public Guid ConfirmationToken { get; init; }
    public long CountRevision { get; init; }
    public long LedgerRevision { get; init; }
    public long OpeningRevision { get; init; }

    /// <summary>
    /// "Confirmo que esta contagem ainda representa o caixa": required only when the count is unchanged since the latest
    /// attempt and the ledger or inherited opening changed.
    /// </summary>
    public bool ConfirmCountStillObserved { get; init; }
}

/// <summary>Recontar: starts a new attempt from the latest one, hiding its calculation until the next Conferir.</summary>
public class RequestBeginSessionRecountJson
{
    public Guid PreviousAttemptId { get; init; }
}

/// <summary>Submit the session with the attempt and revisions the caller confirmed.</summary>
public class RequestSubmitSessionJson
{
    public Guid AttemptId { get; init; }
    public long CountRevision { get; init; }
    public long LedgerRevision { get; init; }
    public long OpeningRevision { get; init; }
}
