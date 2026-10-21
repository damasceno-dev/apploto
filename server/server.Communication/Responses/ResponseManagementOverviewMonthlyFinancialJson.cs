using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix row 12: the selected month's totals from <c>GET /report/monthly-financial/{Year}/{Month}</c>. Separate
/// figures, never summed together; a figure whose marker is unset is null. <c>NotConfigured</c> when neither marker is.
/// </summary>
public class ResponseManagementOverviewMonthlyFinancialJson
{
    public OverviewSectionStatus Status { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public bool IsPartialMonth { get; init; }
    public decimal OfficialVarianceTotal { get; init; }
    public int OfficialCloseCount { get; init; }
    public bool IsBolaoServiceFeeConfigured { get; init; }
    public decimal? BolaoServiceFeeTotal { get; init; }
    public bool IsCommercialLeftoverConfigured { get; init; }
    public decimal? CommercialLeftoverTotal { get; init; }
    public decimal HoursTotal { get; init; }
    public bool ContainsInProgress { get; init; }
}
