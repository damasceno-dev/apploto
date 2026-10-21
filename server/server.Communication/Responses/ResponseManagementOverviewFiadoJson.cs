using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix rows 5–6: who owes the shop and whom it owes, as of today (debt and credit never offset), and how old the
/// FIFO-netted unpaid debt is. Drill-down: <c>GET /report/fiado/balance</c> and <c>GET /report/fiado/aging</c>.
/// </summary>
public class ResponseManagementOverviewFiadoJson
{
    public OverviewSectionStatus Status { get; init; }
    public DateTime AsOfDate { get; init; }

    /// <summary>Clients with a non-zero balance (debtors plus creditors).</summary>
    public int TotalCount { get; init; }
    public int DebtorCount { get; init; }
    public decimal TotalDebt { get; init; }
    public int CreditorCount { get; init; }

    /// <summary>Client credit as a positive magnitude; never subtracted from <see cref="TotalDebt"/>.</summary>
    public decimal TotalCredit { get; init; }
    public decimal AgingOutstandingTotal { get; init; }
    public IReadOnlyList<ResponseAgingBucketTotalJson> AgingByBucket { get; init; } = [];
    public DateTime? OldestUnpaidDate { get; init; }
    public int PreviewLimit { get; init; }

    /// <summary>Largest debtors: balance DESC, client name, client id.</summary>
    public IReadOnlyList<ResponseFiadoClientBalanceItemJson> TopDebtors { get; init; } = [];
}
