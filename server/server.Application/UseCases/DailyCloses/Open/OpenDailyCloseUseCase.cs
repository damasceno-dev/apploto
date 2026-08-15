using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.Open;

public class OpenDailyCloseUseCase(
    IAuthenticationService authenticationService,
    IAccountsRepository accountsRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    IDailyCloseWorkflowGuard workflowGuard,
    LockDateGuard lockDateGuard,
    IDailyClosesRepository dailyClosesRepository,
    IDailyCloseAccountCoordination accountCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseDailyCloseJson> Execute(
        RequestOpenDailyCloseJson request,
        CancellationToken ct = default)
    {
        Validate(request);
        ct.ThrowIfCancellationRequested();

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId, ct);
        var callerOperator = memberScope.LinkedOperator;

        var accountKey = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(
            request.AccountId,
            branchUser.BranchId,
            ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        await using var coordination = await accountCoordination.Acquire(
            branchUser.BranchId,
            accountKey.Id,
            ct);

        var account = await accountsRepository.GetActiveByIdAndBranchId(
            request.AccountId,
            branchUser.BranchId,
            ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, memberScope, account.Id);

        if (account.Type != AccountType.Terminal)
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_ACCOUNT_NOT_TERMINAL);

        workflowGuard.EnsureCanOpen(branchUser, callerOperator, request.Date);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            request.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION,
            ct);

        var dailyClose = request.ToDomain(branchUser.BranchId, branchUser.UserId);
        await dailyClosesRepository.Add(dailyClose, ct);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return dailyClose.ToResponse(account, branchUser.User);
    }

    private static void Validate(RequestOpenDailyCloseJson request)
    {
        var result = new OpenDailyCloseFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
