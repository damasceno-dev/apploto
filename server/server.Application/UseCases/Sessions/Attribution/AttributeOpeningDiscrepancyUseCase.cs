using FluentValidation;
using server.Application.Services.Settings;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Attribution;

/// <summary>
/// AttributeOpeningDiscrepancy (§6.20): a Manager/Admin attributes the exact current comparison revision to the
/// outgoing operator (only with an evidenced predecessor), the incoming operator, or neither, with a reason.
/// Append-only; a revised comparison invalidates the personal projection until attributed again. Dates inside the
/// lock are refused until the after-lock adjustment of M9.7 P7 ships.
/// </summary>
public class AttributeOpeningDiscrepancyUseCase(
    IAuthenticationService authenticationService,
    ISessionOpeningDiscrepanciesRepository discrepanciesRepository,
    IDailyClosesRepository dailyClosesRepository,
    Services.Sessions.SessionEventRecorder eventRecorder,
    Services.Sessions.SessionCommandRunner commandRunner,
    LockDateGuard lockDateGuard,
    Services.Transactions.IBranchClock branchClock)
{
    public async Task<ResponseOpeningDiscrepancyJson> Execute(
        Guid discrepancyId,
        RequestAttributeOpeningDiscrepancyJson request,
        uint expectedVersion,
        CancellationToken ct = default)
    {
        var validation = new AttributeOpeningDiscrepancyFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        SessionCustodyRules.EnsureManager(branchUser);
        var key = await discrepanciesRepository.GetByIdAndBranchId(discrepancyId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPENING_DISCREPANCY_NOT_FOUND);
        var sessionKey = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(key.SessionId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);

        return await commandRunner.RunGuarded<ResponseOpeningDiscrepancyJson>(
            branchUser,
            [sessionKey.AccountId],
            async () =>
            {
                var discrepancy = await discrepanciesRepository.GetByIdAndBranchId(discrepancyId, branchUser.BranchId, ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.OPENING_DISCREPANCY_NOT_FOUND);
                if (discrepancy.Version != expectedVersion || discrepancy.CurrentRevision != request.DiscrepancyRevision)
                    throw new ConflictException(ResourcesErrorMessages.OPENING_DISCREPANCY_STALE);
                if (discrepancy.CurrentTotalDelta == 0m)
                    throw new ConflictException(ResourcesErrorMessages.OPENING_DISCREPANCY_MATCHED);
                if (request.Target == OpeningDiscrepancyAttribution.Outgoing && discrepancy.PredecessorSessionId is null)
                    throw new OnValidationException([ResourcesErrorMessages.OPENING_DISCREPANCY_TARGET_UNAVAILABLE]);

                await lockDateGuard.EnsureNotLocked(
                    branchUser.BranchId,
                    sessionKey.Date,
                    ResourcesErrorMessages.OPENING_DISCREPANCY_LOCKED,
                    ct);
                if (request.Target == OpeningDiscrepancyAttribution.Outgoing)
                {
                    var predecessor = await dailyClosesRepository.GetByIdAndBranchIdAsNoTracking(
                        discrepancy.PredecessorSessionId!.Value,
                        branchUser.BranchId,
                        ct);
                    if (predecessor?.CustodianOperatorId is null)
                        throw new OnValidationException([ResourcesErrorMessages.OPENING_DISCREPANCY_TARGET_UNAVAILABLE]);
                    await lockDateGuard.EnsureNotLocked(
                        branchUser.BranchId,
                        predecessor.Date,
                        ResourcesErrorMessages.OPENING_DISCREPANCY_LOCKED,
                        ct);
                }

                var previous = await discrepanciesRepository.GetLatestAttributionAsNoTracking(discrepancy.Id, ct);
                var now = branchClock.UtcNow();
                var reason = request.Reason!.Trim();
                await discrepanciesRepository.AddAttribution(new OpeningDiscrepancyAttributionEvent
                {
                    DiscrepancyId = discrepancy.Id,
                    DiscrepancyRevision = discrepancy.CurrentRevision,
                    Target = request.Target,
                    Reason = reason,
                    ActorUserId = branchUser.UserId,
                    SupersedesEventId = previous?.Id,
                    CreatedAt = now
                }, ct);
                discrepancy.Attribution = request.Target;
                discrepancy.AttributedRevision = discrepancy.CurrentRevision;
                await eventRecorder.Record(
                    sessionKey,
                    SessionEventKind.AttributionRecorded,
                    branchUser.User,
                    now,
                    new { DiscrepancyId = discrepancy.Id, discrepancy.CurrentRevision, request.Target, discrepancy.CurrentTotalDelta },
                    reason,
                    discrepancy.Id,
                    ct);

                return async () =>
                {
                    var saved = await discrepanciesRepository.GetBySessionIdAsNoTracking(branchUser.BranchId, discrepancy.SessionId, ct);
                    return saved!.ToResponse();
                };
            },
            ct);
    }
}

public class AttributeOpeningDiscrepancyFluentValidation
    : FluentValidation.AbstractValidator<RequestAttributeOpeningDiscrepancyJson>
{
    public AttributeOpeningDiscrepancyFluentValidation()
    {
        RuleFor(request => request.Target)
            .IsInEnum()
            .NotEqual(OpeningDiscrepancyAttribution.Unattributed)
            .WithMessage(ResourcesErrorMessages.OPENING_DISCREPANCY_TARGET_INVALID);
        RuleFor(request => request.DiscrepancyRevision)
            .GreaterThan(0)
            .WithMessage(ResourcesErrorMessages.OPENING_DISCREPANCY_STALE);
        RuleFor(request => request.Reason).RequiredReason();
    }
}
