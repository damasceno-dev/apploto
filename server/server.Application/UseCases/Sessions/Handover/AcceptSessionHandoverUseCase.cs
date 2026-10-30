using FluentValidation;
using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Handover;

/// <summary>
/// AcceptSessionHandover (§6.20): the intended incoming operator acknowledges what they observe (matching or
/// different). Outgoing ends HandedOver and incoming starts Active in one commit.
/// </summary>
public class AcceptSessionHandoverUseCase(
    IAuthenticationService authenticationService,
    ISessionHandoversRepository sessionHandoversRepository,
    ISessionOpeningDiscrepanciesRepository discrepanciesRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    SessionHandoverAcceptor acceptor,
    SessionCommandRunner commandRunner,
    SessionResponseLoader responseLoader)
{
    private const string IdempotencyEndpoint = "POST /session/handover/{id}/accept";

    public async Task<ResponseAcceptSessionHandoverJson> Execute(
        Guid handoverId,
        RequestAcceptSessionHandoverJson request,
        uint expectedVersion,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        var validation = new AcceptSessionHandoverFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).ToList());

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var handoverKey = await sessionHandoversRepository.GetByIdAndBranchIdAsNoTracking(handoverId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_FOUND);

        return await commandRunner.Run<object, ResponseAcceptSessionHandoverJson>(
            IdempotencyEndpoint,
            idempotencyKey,
            branchUser,
            new { HandoverId = handoverId, ExpectedVersion = expectedVersion, Request = request },
            [handoverKey.AccountId],
            async () =>
            {
                var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
                var handover = await sessionHandoversRepository.GetByIdAndBranchId(handoverId, branchUser.BranchId, ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.SESSION_HANDOVER_NOT_FOUND);

                var accepted = await acceptor.Accept(
                    handover,
                    expectedVersion,
                    branchUser,
                    memberScope,
                    request.Items,
                    request.ExplicitZero,
                    request.Reason,
                    ct);

                return async () =>
                {
                    var saved = await sessionHandoversRepository.GetByIdAndBranchIdAsNoTracking(handoverId, branchUser.BranchId, ct);
                    return (accepted.Incoming.Session.Id, new ResponseAcceptSessionHandoverJson
                    {
                        Handover = saved!.ToResponse(),
                        OutgoingSession = await responseLoader.Load(accepted.Outgoing.Id, branchUser.BranchId, ct),
                        Incoming = await responseLoader.LoadStart(accepted.Incoming, branchUser.BranchId, discrepanciesRepository, ct)
                    });
                };
            },
            ct);
    }
}

public class AcceptSessionHandoverFluentValidation : FluentValidation.AbstractValidator<RequestAcceptSessionHandoverJson>
{
    public AcceptSessionHandoverFluentValidation()
    {
        RuleFor(request => request.Items).OpeningItems();
        RuleFor(request => request.Reason).OptionalReason();
    }
}
