namespace server.Communication.Requests;

public class RequestCreateTerminalAccountJson
{
    public string Name { get; init; } = string.Empty;
    public string? Institution { get; init; }
    public string? Number { get; init; }
    public Guid? ExistingTabAccountId { get; init; }
    public bool CreateTabAccount { get; init; }

    /// <summary>
    /// The physical CAIXA terminal of this cashbox (M9.6 Phase 2). Omitted: a terminal named after the cashbox is
    /// created with it. A cashbox needs a terminal before its first session.
    /// </summary>
    public Guid? TerminalId { get; init; }
}
