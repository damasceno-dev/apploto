using server.Application.Services.DailyCloses;
using server.Application.UseCases.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions;

/// <summary>Reloads a session inside the command transaction so responses carry committed versions and names.</summary>
public sealed class SessionResponseLoader(
    IDailyClosesRepository dailyClosesRepository,
    ICashVarianceProductResolver cashVarianceProductResolver)
{
    public async Task<ResponseDailyCloseJson> Load(Guid sessionId, Guid branchId, CancellationToken ct = default)
    {
        var session = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(sessionId, branchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchId, ct);
        return session.ToResponse(cashVarianceProductId);
    }

    public async Task<ResponseStartSessionJson> LoadStart(
        Services.Sessions.SessionStartResult started,
        Guid branchId,
        ISessionOpeningDiscrepanciesRepository discrepanciesRepository,
        CancellationToken ct = default)
    {
        SessionOpeningDiscrepancy? discrepancy = started.Discrepancy is null
            ? null
            : await discrepanciesRepository.GetBySessionIdAsNoTracking(branchId, started.Session.Id, ct);
        return new ResponseStartSessionJson
        {
            Session = await Load(started.Session.Id, branchId, ct),
            SessionOpeningId = started.Opening.Id,
            OpeningDiscrepancy = discrepancy?.ToResponse()
        };
    }
}
