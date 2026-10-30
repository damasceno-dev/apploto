using FluentValidation;
using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.CustodyException;

/// <summary>
/// ResolveCustodyException (§6.20, owner decision F1): a Manager/Admin resolves a stalled or absent custody with a
/// mandatory reason. <c>Finish</c> ends a Submitted or still-Draft session (the Draft stays a submit-gap and
/// month-lock blocker; nothing is fabricated). <c>AcceptAndStart</c> accepts the pending offer for its intended
/// incoming operator, or — with no offer — finishes the Submitted session and starts the named operator from the
/// vector the manager observed. Neither impersonates an acknowledgment of the absent person.
/// </summary>
public class ResolveCustodyExceptionUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IAccountsRepository accountsRepository,
    ISessionHandoversRepository sessionHandoversRepository,
    ISessionOpeningDiscrepanciesRepository discrepanciesRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    SessionHandoverAcceptor acceptor,
    SessionStarter sessionStarter,
    SessionOpeningResolver openingResolver,
    SessionEventRecorder eventRecorder,
    SessionCommandRunner commandRunner,
    SessionResponseLoader responseLoader,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    private const string IdempotencyEndpoint = "POST /session/{id}/custody-exception";

    public async Task<ResponseResolveCustodyExceptionJson> Execute(
        Guid sessionId,
        RequestResolveCustodyExceptionJson request,
        uint expectedVersion,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        var validation = new ResolveCustodyExceptionFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        SessionCustodyRules.EnsureManager(branchUser);
        var sessionKey = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(sessionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
        var reason = request.Reason!.Trim();

        return await commandRunner.Run<object, ResponseResolveCustodyExceptionJson>(
            IdempotencyEndpoint,
            idempotencyKey,
            branchUser,
            new { SessionId = sessionId, ExpectedVersion = expectedVersion, Request = request },
            [sessionKey.AccountId],
            async () =>
            {
                var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
                var session = await dailyClosesRepository.GetByIdAndBranchId(sessionId, branchUser.BranchId, ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
                SessionCustodyRules.EnsureActiveCustody(session);
                if (session.Version != expectedVersion)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_STALE_WRITE);

                var pending = await sessionHandoversRepository.GetUnresolvedByOutgoingSessionId(branchUser.BranchId, session.Id, ct);
                var now = branchClock.UtcNow();

                if (request.Action == CustodyExceptionAction.Finish)
                {
                    if (pending is not null)
                        throw new ConflictException(ResourcesErrorMessages.SESSION_FINISH_PENDING_HANDOVER);

                    // F1: a Draft session left Active may be finished; it remains review work and a blocker.
                    session.CustodyState = SessionCustodyState.Finished;
                    session.EndedAt = now;
                    session.UpdatedAt = now;
                    session.UpdatedByUserId = branchUser.UserId;
                    await eventRecorder.Record(
                        session,
                        SessionEventKind.CustodyExceptionResolved,
                        branchUser.User,
                        now,
                        new { Action = request.Action, EndedAt = now, ReviewStatus = session.Status },
                        reason,
                        ct: ct);

                    return async () => (session.Id, new ResponseResolveCustodyExceptionJson
                    {
                        OutgoingSession = await responseLoader.Load(session.Id, branchUser.BranchId, ct)
                    });
                }

                SessionStartResult incoming;
                if (pending is not null)
                {
                    var accepted = await acceptor.AcceptAsManagerException(
                        pending,
                        branchUser,
                        memberScope,
                        request.IncomingOperatorId!.Value,
                        request.Items,
                        request.ExplicitZero,
                        reason,
                        ct);
                    incoming = accepted.Incoming;
                }
                else
                {
                    if (session.Status != DailyCloseStatus.Submitted)
                        throw new ConflictException(ResourcesErrorMessages.SESSION_FINISH_REQUIRES_SUBMITTED);

                    session.CustodyState = SessionCustodyState.Finished;
                    session.EndedAt = now;
                    session.UpdatedAt = now;
                    session.UpdatedByUserId = branchUser.UserId;
                    await eventRecorder.Record(
                        session,
                        SessionEventKind.CustodyExceptionResolved,
                        branchUser.User,
                        now,
                        new { Action = request.Action, EndedAt = now, request.IncomingOperatorId },
                        reason,
                        ct: ct);
                    await unitOfWork.Commit(ct);

                    var cashbox = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(session.AccountId, branchUser.BranchId, ct)
                        ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
                    var proposal = await openingResolver.ProposeForNextSession(branchUser.BranchId, cashbox.Id, ct);
                    incoming = await sessionStarter.Start(new SessionStartCommand(
                        cashbox,
                        branchUser,
                        memberScope,
                        request.IncomingOperatorId,
                        SessionOrigin.ManagerException,
                        request.ExplicitZero,
                        request.Items,
                        reason,
                        proposal.SourceKind,
                        proposal.SourceId,
                        proposal.SourceRevision,
                        ForceManagerException: true), ct);
                }

                return async () => (session.Id, new ResponseResolveCustodyExceptionJson
                {
                    OutgoingSession = await responseLoader.Load(session.Id, branchUser.BranchId, ct),
                    Incoming = await responseLoader.LoadStart(incoming, branchUser.BranchId, discrepanciesRepository, ct)
                });
            },
            ct);
    }
}

public class ResolveCustodyExceptionFluentValidation : FluentValidation.AbstractValidator<RequestResolveCustodyExceptionJson>
{
    public ResolveCustodyExceptionFluentValidation()
    {
        RuleFor(request => request.Action).IsInEnum().WithMessage(ResourcesErrorMessages.SESSION_EXCEPTION_ACTION_INVALID);
        RuleFor(request => request.Reason).RequiredReason();
        RuleFor(request => request.IncomingOperatorId)
            .NotEmpty()
            .When(request => request.Action == CustodyExceptionAction.AcceptAndStart)
            .WithMessage(ResourcesErrorMessages.SESSION_EXCEPTION_INCOMING_REQUIRED);
        RuleFor(request => request.Items).OpeningItems();
    }
}
