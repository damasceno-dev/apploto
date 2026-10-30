using server.Application.Services.Members;
using server.Application.Services.Sessions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Sessions.Start;

/// <summary>
/// StartSession (§6.20): Conferir abertura e iniciar without a financial operation. The first operation may instead
/// carry the same acknowledgment on its own financial command.
/// </summary>
public class StartSessionUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    ISessionOpeningDiscrepanciesRepository discrepanciesRepository,
    SessionStarter sessionStarter,
    SessionCommandRunner commandRunner,
    SessionResponseLoader responseLoader)
{
    private const string IdempotencyEndpoint = "POST /session/start";

    public async Task<ResponseStartSessionJson> Execute(
        RequestStartSessionJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        Validate(request);
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var opening = request.Opening!;

        return await commandRunner.Run<RequestStartSessionJson, ResponseStartSessionJson>(
            IdempotencyEndpoint,
            idempotencyKey,
            branchUser,
            request,
            [request.AccountId],
            async () =>
            {
                var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
                var cashbox = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(request.AccountId, branchUser.BranchId, ct)
                    ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

                var started = await sessionStarter.Start(new SessionStartCommand(
                    cashbox,
                    branchUser,
                    memberScope,
                    opening.CustodianOperatorId,
                    SessionOrigin.Started,
                    opening.ExplicitZero,
                    opening.Items,
                    opening.Reason,
                    opening.SourceKind,
                    opening.SourceId,
                    opening.SourceRevision), ct);

                return async () => (started.Session.Id,
                    await responseLoader.LoadStart(started, branchUser.BranchId, discrepanciesRepository, ct));
            },
            ct);
    }

    private static void Validate(RequestStartSessionJson request)
    {
        var result = new StartSessionFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }
}
