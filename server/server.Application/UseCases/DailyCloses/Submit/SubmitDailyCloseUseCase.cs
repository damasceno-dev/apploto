using server.Application.Services.DailyCloses;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.DailyCloses.Submit;

public class SubmitDailyCloseUseCase(
    IAuthenticationService authenticationService,
    IDailyClosesRepository dailyClosesRepository,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    MemberAccountScopeGuard memberAccountScopeGuard,
    IDailyCloseWorkflowGuard workflowGuard,
    LockDateGuard lockDateGuard,
    ICashVarianceProductResolver cashVarianceProductResolver,
    ICashVarianceCalculator cashVarianceCalculator,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseDailyCloseJson> Execute(Guid dailyCloseId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var close = await dailyClosesRepository.GetByIdAndBranchId(dailyCloseId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.DAILYCLOSE_NOT_FOUND);

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
        var callerOperator = memberScope.LinkedOperator;

        memberAccountScopeGuard.EnsureMemberCanActOnAccount(branchUser.Role, memberScope, close.AccountId);

        workflowGuard.EnsureCanSubmit(close, branchUser, callerOperator);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            close.Date,
            ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);

        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchUser.BranchId);
        var cashVariance = await cashVarianceCalculator.CalculateAsync(
            branchUser.BranchId,
            close.AccountId,
            close.Date,
            close.Id,
            cashVarianceProductId,
            CancellationToken.None);

        var existing = close.Items.FirstOrDefault(item =>
            item.ProductId == cashVarianceProductId &&
            item.Active);

        if (existing is not null)
        {
            existing.Value = cashVariance;
        }
        else
        {
            close.Items.Add(new DailyCloseItem
            {
                Id = Guid.Empty,
                DailyCloseId = close.Id,
                ProductId = cashVarianceProductId,
                Value = cashVariance
            });
        }

        var now = branchClock.UtcNow();

        close.SubmittedByOperatorId = callerOperator?.Id;
        close.Status = DailyCloseStatus.Submitted;
        close.SubmittedAt = now;
        close.UpdatedAt = now;
        close.UpdatedByUserId = branchUser.UserId;

        await unitOfWork.Commit();

        return close.ToResponse();
    }
}
