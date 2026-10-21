using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// Matrix row 3: the selected month's official variance, the same rows and figures as
/// <c>GET /report/cash-variance?DateFrom&amp;DateTo</c>, plus the largest absolute variances (absolute value DESC,
/// <c>Date DESC</c>, close id).
/// </summary>
public class ResponseManagementOverviewCashVarianceJson
{
    public OverviewSectionStatus Status { get; init; }
    public DateTime DateFrom { get; init; }
    public DateTime DateTo { get; init; }
    public int TotalCount { get; init; }
    public decimal TotalVariance { get; init; }
    public decimal MeanVariance { get; init; }
    public decimal MaxVariance { get; init; }
    public decimal MinVariance { get; init; }
    public int SubmittedCount { get; init; }
    public int ApprovedCount { get; init; }
    public int RejectedCount { get; init; }
    public int PreviewLimit { get; init; }
    public IReadOnlyList<ResponseCashVarianceSummaryItemJson> LargestVariances { get; init; } = [];
}
