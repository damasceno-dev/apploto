using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Communication.Requests;
using server.Communication.Responses;
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
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseDailyCloseJson> Execute(RequestOpenDailyCloseJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
        var callerOperator = memberScope.LinkedOperator;

        var account = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(
            request.AccountId,
            branchUser.BranchId);

        if (account is null)
            throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        memberAccountScopeGuard.EnsureMemberCanActOnAccount(
            branchUser.Role,
            memberScope,
            request.AccountId);

        workflowGuard.EnsureCanOpen(
            branchUser,
            callerOperator,
            request.AccountId,
            request.Date);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            request.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);

        var dailyClose = request.ToDomain(
            branchId: branchUser.BranchId,
            submittedByOperatorId: callerOperator?.Id);

        await dailyClosesRepository.Add(dailyClose);
        await unitOfWork.Commit();

        return dailyClose.ToResponse(account, callerOperator);
    }

    private static void Validate(RequestOpenDailyCloseJson request)
    {
        var result = new OpenDailyCloseFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
