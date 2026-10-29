using server.Application.Services.Sessions;
using server.Communication.Requests;
using server.Domain.Entities;

namespace server.Application.Services.DailyCloses;

/// <summary>
/// §6.12 CashVariance = TotalClosing − TotalOpening − (In − Out) for one session: its own count, its acknowledged
/// (or inherited) opening and the active rows bound to it. The arithmetic is unchanged; only its inputs moved to the
/// session.
/// </summary>
public interface ICashVarianceCalculator
{
    Task<decimal> CalculateAsync(DailyClose session, Guid cashVarianceProductId, CancellationToken ct);

    Task<decimal> CalculateCandidateAsync(
        DailyClose session,
        IReadOnlyList<RequestUpsertDailyCloseItemJson> candidateValues,
        Guid cashVarianceProductId,
        CancellationToken ct);

    Task<decimal> CalculateWithOpeningAsync(
        DailyClose session,
        ResolvedSessionOpening opening,
        Guid cashVarianceProductId,
        CancellationToken ct);
}
