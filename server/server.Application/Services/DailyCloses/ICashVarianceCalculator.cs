using server.Communication.Requests;
using server.Domain.Entities;

namespace server.Application.Services.DailyCloses;

public interface ICashVarianceCalculator
{
    Task<decimal> CalculateAsync(
        Guid branchId,
        Guid accountId,
        DateTime branchLocalDate,
        Guid currentDailyCloseId,
        Guid cashVarianceProductId,
        CancellationToken ct);

    Task<decimal> CalculateCandidateAsync(
        Guid branchId,
        Guid accountId,
        DateTime branchLocalDate,
        IReadOnlyList<RequestUpsertDailyCloseItemJson> candidateValues,
        Guid cashVarianceProductId,
        CancellationToken ct);

    Task<decimal> CalculateWithOpeningSourceAsync(
        Guid branchId,
        Guid accountId,
        DateTime branchLocalDate,
        Guid currentDailyCloseId,
        Guid cashVarianceProductId,
        DailyClose? openingSource,
        CancellationToken ct);
}
