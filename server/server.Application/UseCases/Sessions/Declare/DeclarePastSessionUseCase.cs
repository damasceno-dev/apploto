using FluentValidation;
using server.Application.Services.Sessions;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Declare;

/// <summary>
/// DeclarePastSession (owner decision F2): a Manager/Admin records a forgotten past period of one cashbox as a
/// Finished, Draft session with ManagerDeclared evidence and an inherited opening — no acknowledgment is attributed
/// to the custodian. Sequence follows interval order; affected successors follow the correction rules.
/// </summary>
public class DeclarePastSessionUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IOperatorsRepository operatorsRepository,
    IDailyClosesRepository dailyClosesRepository,
    SessionChainService chainService,
    SessionEventRecorder eventRecorder,
    SessionCommandRunner commandRunner,
    SessionResponseLoader responseLoader,
    LockDateGuard lockDateGuard,
    IBranchClock branchClock)
{
    private const string IdempotencyEndpoint = "POST /session/declare";

    public async Task<ResponseSessionChainChangeJson> Execute(
        RequestDeclarePastSessionJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        var validation = new DeclarePastSessionFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        SessionCustodyRules.EnsureManager(branchUser);

        return await commandRunner.Run<RequestDeclarePastSessionJson, ResponseSessionChainChangeJson>(
            IdempotencyEndpoint,
            idempotencyKey,
            branchUser,
            request,
            [request.AccountId],
            async () =>
            {
                var (startedAt, endedAt, businessDate) = SessionWindow.Resolve(request.StartedAt, request.EndedAt, branchClock);
                var cashbox = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.AccountId, branchUser.BranchId, ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);
                if (cashbox.Type != AccountType.Terminal)
                    throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_ACCOUNT_NOT_TERMINAL);
                if (cashbox.TerminalId is null)
                    throw new ConflictException(ResourcesErrorMessages.SESSION_CASHBOX_TERMINAL_REQUIRED);
                if (await dailyClosesRepository.HasUnresolvedHistoryByAccountIdAsNoTracking(branchUser.BranchId, cashbox.Id, ct))
                    throw new ConflictException(ResourcesErrorMessages.SESSION_UNRESOLVED_HISTORY);

                // The custodian is any operator of the branch, active or not; no Member authority derives from it.
                var custodian = await operatorsRepository.GetByIdAndBranchIdAsNoTracking(
                    request.CustodianOperatorId,
                    branchUser.BranchId,
                    ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_CUSTODIAN_NOT_FOUND);

                await lockDateGuard.EnsureNotLocked(
                    branchUser.BranchId,
                    businessDate,
                    ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION,
                    ct);

                var chain = (await dailyClosesRepository.ListReadyByAccountIdOrderedByStart(branchUser.BranchId, cashbox.Id, ct)).ToList();
                if (chain.Any(session => SessionWindow.Overlaps(session.StartedAt!.Value, session.EndedAt, startedAt, endedAt)))
                    throw new ConflictException(ResourcesErrorMessages.SESSION_DECLARE_OVERLAP);

                var now = branchClock.UtcNow();
                var session = new DailyClose
                {
                    BranchId = branchUser.BranchId,
                    AccountId = cashbox.Id,
                    Date = businessDate,
                    Status = DailyCloseStatus.Draft,
                    OpenedByUserId = branchUser.UserId,
                    MappingState = SessionMappingState.Ready,
                    Origin = SessionOrigin.ManagerDeclared,
                    CustodyState = SessionCustodyState.Finished,
                    CustodianOperatorId = custodian.Id,
                    CustodianUserIdAtStart = custodian.UserId,
                    TerminalId = cashbox.TerminalId,
                    Sequence = chain.Select(existing => existing.Sequence ?? 0).DefaultIfEmpty(0).Max() + 1,
                    StartedAt = startedAt,
                    EndedAt = endedAt,
                    CreatedAt = now
                };
                await dailyClosesRepository.Add(session, ct);
                chain.Add(session);

                var effects = await chainService.Resequence(chain, session, branchUser.User, now, ct);
                await eventRecorder.Record(
                    session,
                    SessionEventKind.SessionDeclared,
                    branchUser.User,
                    now,
                    new
                    {
                        After = new { session.StartedAt, session.EndedAt, session.Date, session.Sequence, CustodianOperatorId = custodian.Id },
                        Affected = effects.Select(effect => effect.Session.Id)
                    },
                    request.Reason!.Trim(),
                    ct: ct);

                return async () => (session.Id, new ResponseSessionChainChangeJson
                {
                    Session = await responseLoader.Load(session.Id, branchUser.BranchId, ct),
                    AffectedSessions = await SessionChainResponses.Load(effects, dailyClosesRepository, branchUser.BranchId, ct)
                });
            },
            ct);
    }
}

public class DeclarePastSessionFluentValidation : FluentValidation.AbstractValidator<RequestDeclarePastSessionJson>
{
    public DeclarePastSessionFluentValidation()
    {
        RuleFor(request => request.AccountId).NotEmpty().WithMessage(ResourcesErrorMessages.ACCOUNT_ID_EMPTY);
        RuleFor(request => request.CustodianOperatorId).NotEmpty().WithMessage(ResourcesErrorMessages.SESSION_CUSTODIAN_NOT_FOUND);
        RuleFor(request => request.Reason).RequiredReason();
        RuleFor(request => request)
            .Must(request => request.EndedAt > request.StartedAt)
            .WithMessage(ResourcesErrorMessages.SESSION_DECLARE_WINDOW_INVALID);
    }
}
