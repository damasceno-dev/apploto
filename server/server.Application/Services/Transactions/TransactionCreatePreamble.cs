using server.Application.Services.Holidays;
using server.Application.Services.Members;
using server.Application.Services.Settings;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Transactions;

public sealed record TransactionCreateContext(
    BranchUser BranchUser,
    Operator? CallerOperator,
    Guid RecordedByOperatorId,
    TransactionType TransactionType,
    DateTime? DueDate = null,
    IReadOnlySet<DateOnly>? BranchHolidays = null);

public class TransactionCreatePreamble(
    IAuthenticationService authenticationService,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    TransactionRecordedByOperatorResolver recordedByOperatorResolver,
    TransactionBranchConsistencyService transactionBranchConsistencyService,
    MemberAccountScopeGuard memberAccountScopeGuard,
    LockDateGuard lockDateGuard,
    IBranchHolidaySource branchHolidaySource)
{
    public async Task<TransactionCreateContext> Resolve(RequestCreateTransactionJson request)
    {
        var context = await ResolveCommon(
            requestedRecordedByOperatorId: request.RecordedByOperatorId,
            accountId: request.AccountId,
            clientId: request.ClientId,
            transactionTypeId: request.TransactionTypeId,
            transactionDate: request.Date);

        if (context.TransactionType.SettlementRule == SettlementRule.OperatorEnteredCheque &&
            request.DueDate is null)
        {
            throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_CHEQUE_REQUIRES_DUE_DATE]);
        }

        var holidayDates = await branchHolidaySource.GetHolidayDatesAsync(context.BranchUser.BranchId);

        var dueDate = DueDateCalculator.Compute(
            context.TransactionType.SettlementRule,
            request.Date,
            request.DueDate,
            holidayDates);

        await lockDateGuard.EnsureNotLocked(
            context.BranchUser.BranchId,
            request.Date,
            ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);

        return context with { DueDate = dueDate, BranchHolidays = holidayDates };
    }

    public async Task<TransactionCreateContext> Resolve(RequestCreateTransactionInstallmentJson request)
    {
        var context = await ResolveCommon(
            requestedRecordedByOperatorId: request.RecordedByOperatorId,
            accountId: request.AccountId,
            clientId: request.ClientId,
            transactionTypeId: request.TransactionTypeId,
            transactionDate: request.Date);

        await lockDateGuard.EnsureNotLocked(
            context.BranchUser.BranchId,
            request.Date,
            ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);

        var holidayDates = await branchHolidaySource.GetHolidayDatesAsync(context.BranchUser.BranchId);

        return context with { BranchHolidays = holidayDates };
    }

    private async Task<TransactionCreateContext> ResolveCommon(
        Guid? requestedRecordedByOperatorId,
        Guid accountId,
        Guid? clientId,
        Guid transactionTypeId,
        DateTime transactionDate)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
        var callerOperator = memberScope.LinkedOperator;

        var recordedByOperatorId = recordedByOperatorResolver.Resolve(
            requestedRecordedByOperatorId,
            branchUser.Role,
            callerOperator);

        var transactionType = await transactionBranchConsistencyService.ResolveAndValidate(
            branchId: branchUser.BranchId,
            accountId: accountId,
            recordedByOperatorId: recordedByOperatorId,
            clientId: clientId,
            transactionTypeId: transactionTypeId);

        memberAccountScopeGuard.EnsureMemberCanActOnAccount(
            branchUser.Role,
            memberScope,
            accountId);

        return new TransactionCreateContext(
            BranchUser: branchUser,
            CallerOperator: callerOperator,
            RecordedByOperatorId: recordedByOperatorId,
            TransactionType: transactionType);
    }
}
