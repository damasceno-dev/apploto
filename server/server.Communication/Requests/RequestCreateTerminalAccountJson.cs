namespace server.Communication.Requests;

public class RequestCreateTerminalAccountJson
{
    public string Name { get; init; } = string.Empty;
    public string? Institution { get; init; }
    public string? Number { get; init; }
    public Guid? ExistingTabAccountId { get; init; }

    /// <summary>
    /// M9.6 Phase 6: a cashbox is created with a new paired fiado (Tab) account by default — omitted or <c>true</c>
    /// creates it, unless <see cref="ExistingTabAccountId"/> pairs an existing one. Explicit <c>false</c> creates the
    /// cashbox unpaired; its fiado sales are refused until it is paired. Pairing grants nobody Tab authority.
    /// </summary>
    public bool? CreateTabAccount { get; init; }

    /// <summary>
    /// The physical CAIXA terminal of this cashbox (M9.6 Phase 2). Omitted: a terminal named after the cashbox is
    /// created with it. A cashbox needs a terminal before its first session.
    /// </summary>
    public Guid? TerminalId { get; init; }
}
