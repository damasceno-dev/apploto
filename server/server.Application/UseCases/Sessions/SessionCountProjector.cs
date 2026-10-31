using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Application.UseCases.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions;

/// <summary>
/// Adds the blind-count state and the caller's permitted actions to a session response (session read, count save,
/// Conferir, Recontar). Reads only: it never allocates a token, creates an attempt or commits.
/// </summary>
public sealed class SessionCountProjector(
    IDailyClosesRepository dailyClosesRepository,
    ICashVarianceProductResolver cashVarianceProductResolver,
    SessionCountService countService,
    SessionAllowedActionsResolver allowedActionsResolver)
{
    public async Task<ResponseDailyCloseJson> Load(
        Guid sessionId,
        BranchUser caller,
        MemberAccountScope scope,
        CancellationToken ct = default)
    {
        var session = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(sessionId, caller.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(caller.BranchId, ct);
        return await Project(session, session.ToResponse(cashVarianceProductId), caller, scope, ct);
    }

    public async Task<ResponseDailyCloseJson> Project(
        DailyClose session,
        ResponseDailyCloseJson response,
        BranchUser caller,
        MemberAccountScope scope,
        CancellationToken ct = default)
    {
        var (count, actions) = await Build(session, caller, scope, ct);
        response.Count = count;
        response.AllowedActions = actions;
        return response;
    }

    public async Task<(ResponseSessionCountJson Count, ResponseSessionAllowedActionsJson Actions)> Build(
        DailyClose session,
        BranchUser caller,
        MemberAccountScope scope,
        CancellationToken ct = default)
    {
        var count = await countService.BuildResponse(session, ct);
        var actions = await allowedActionsResolver.Resolve(
            session,
            caller,
            scope,
            count.CandidateState,
            count.LatestAttemptId is not null,
            ct);
        return (count, actions);
    }
}
