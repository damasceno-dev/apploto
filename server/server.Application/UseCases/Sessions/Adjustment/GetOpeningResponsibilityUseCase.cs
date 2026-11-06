using server.Application.Services.Sessions;
using FluentValidation;
using server.Application.Services.Settings;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Adjustment;

public class GetOpeningResponsibilityUseCase(
    IAuthenticationService authenticationService,
    ISessionOpeningDiscrepanciesRepository discrepancies,
    IOpeningDiscrepancyAdjustmentsRepository adjustments,
    IDailyClosesRepository sessions,
    ILockDateReader lockDateReader,
    IReportReadSnapshot snapshot)
{
    public async Task<ResponseOpeningResponsibilityJson> Execute(Guid id, RequestOpeningResponsibilityJson request, CancellationToken ct = default)
    {
        var actor = await authenticationService.GetAuthenticatedBranchUser();
        SessionCustodyRules.EnsureManager(actor);
        var validation = new OpeningResponsibilityFluentValidation().Validate(request);
        if (!validation.IsValid) throw new OnValidationException(validation.Errors.Select(e => e.ErrorMessage).ToList());
        await using var read = await snapshot.Begin(ct);
        var source = await discrepancies.GetByIdAndBranchIdAsNoTracking(id, actor.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPENING_DISCREPANCY_NOT_FOUND);
        var session = await sessions.GetByIdAndBranchIdAsNoTracking(source.SessionId, actor.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_NOT_FOUND);
        var predecessor = source.PredecessorSessionId is { } predecessorId
            ? await sessions.GetByIdAndBranchIdAsNoTracking(predecessorId, actor.BranchId, ct) : null;
        var latestAttribution = await OpeningAttributionReader.Read(discrepancies, source.Id, ct);
        var latest = await adjustments.Latest(actor.BranchId, source.Id, ct);
        var history = await adjustments.List(actor.BranchId, source.Id, request.AfterSequence, request.PageSize + 1, ct);
        var original = source.ToResponse();
        // Target availability is evidence, not a guess from who recorded or submitted a row.
        original.AllowedTargets.RemoveAll(t =>
            t == OpeningDiscrepancyAttribution.Outgoing && predecessor?.CustodianOperatorId == null ||
            t == OpeningDiscrepancyAttribution.Incoming && session.CustodianOperatorId == null);
        var lockDate = await lockDateReader.Read(actor.BranchId, ct);
        return new ResponseOpeningResponsibilityJson
        {
            Discrepancy = original, SourceDate = session.Date,
            SourceLocked = session.Date <= lockDate || predecessor != null && predecessor.Date <= lockDate,
            LatestAttributionId = latestAttribution?.Id, LatestAdjustment = latest?.ToResponse(),
            Adjustments = history.Take(request.PageSize).Select(a => a.ToResponse()).ToList(),
            NextAfterSequence = history.Count > request.PageSize ? history[request.PageSize - 1].Sequence : null
        };
    }
}

public class OpeningResponsibilityFluentValidation : AbstractValidator<RequestOpeningResponsibilityJson>
{
    public OpeningResponsibilityFluentValidation()
    {
        RuleFor(r => r.AfterSequence).GreaterThanOrEqualTo(0).WithMessage(ResourcesErrorMessages.REPORT_PAGE_INVALID);
        RuleFor(r => r.PageSize).InclusiveBetween(1, 200).WithMessage(ResourcesErrorMessages.REPORT_PAGE_SIZE_INVALID);
    }
}
