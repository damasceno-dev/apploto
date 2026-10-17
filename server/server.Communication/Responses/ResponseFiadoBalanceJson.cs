namespace server.Communication.Responses;

public class ResponseFiadoBalanceJson
{
    public DateTime AsOfDate { get; init; }
    public IReadOnlyList<ResponseFiadoClientBalanceItemJson> Items { get; init; } = [];

    /// <summary>
    /// The signed sum of every listed client's <c>Out − In</c>: a client in credit lowers it, so it is a net figure,
    /// not total debt. Unchanged by M11 Phase 3.1; use <see cref="TotalDebt"/> and <see cref="TotalCredit"/> for the split.
    /// </summary>
    public decimal TotalOutstanding { get; init; }

    /// <summary>Clients with a positive balance (they owe) and the sum of those balances (M11 Phase 3.1).</summary>
    public int DebtorCount { get; init; }
    public decimal TotalDebt { get; init; }

    /// <summary>Clients with a negative balance (in credit) and the credit as a positive amount (M11 Phase 3.1).</summary>
    public int CreditorCount { get; init; }
    public decimal TotalCredit { get; init; }
}
