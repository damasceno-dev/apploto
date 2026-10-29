using server.Application.Services.Sessions;
using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Sessions;

internal static class SessionChainResponses
{
    public static async Task<List<ResponseAffectedSessionJson>> Load(
        IReadOnlyList<SessionChainEffect> effects,
        IDailyClosesRepository dailyClosesRepository,
        Guid branchId,
        CancellationToken ct)
    {
        var responses = new List<ResponseAffectedSessionJson>();
        foreach (var effect in effects)
        {
            var saved = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(effect.Session.Id, branchId, ct);
            responses.Add(new ResponseAffectedSessionJson
            {
                SessionId = effect.Session.Id,
                Version = saved?.Version ?? effect.Session.Version,
                Sequence = saved?.Sequence,
                Status = saved?.Status ?? effect.Session.Status,
                OpeningRecheckRequired = effect.OpeningRecheckRequired,
                DiscrepancyRevised = effect.DiscrepancyRevised
            });
        }

        return responses;
    }
}
