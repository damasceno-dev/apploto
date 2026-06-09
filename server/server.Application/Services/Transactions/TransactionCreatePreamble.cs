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

/// <summary>
/// Resolved, branch-scoped context shared by transaction create flows after request-shape validation
/// and before row construction. It contains the authenticated branch user, the resolved operator
/// context, the validated transaction type/category, and the account data needed by write and preview
/// use cases. Single-transaction creates also receive the final computed due date; installment creates
/// receive the branch holiday set so the installment plan builder can compute many due dates later.
/// </summary>
/// <param name="BranchUser">The authenticated user membership for the current branch, including role, user id, and branch id.</param>
/// <param name="CallerOperator">The active operator linked to the authenticated user in this branch, when one exists.</param>
/// <param name="RecordedByOperatorId">The final operator id that owns the transaction context after role-based resolution.</param>
/// <param name="TransactionType">The validated active transaction type for the branch, loaded with its category.</param>
/// <param name="DueDate">The final due date for a single transaction create; null for installment preamble resolution.</param>
/// <param name="BranchHolidays">The active branch holiday dates used by due-date and installment calculations.</param>
/// <param name="Account">The validated active account for the branch. Create preview uses this to classify the hypothetical row.</param>
public sealed record TransactionCreateContext(
    BranchUser BranchUser,
    Operator? CallerOperator,
    Guid RecordedByOperatorId,
    TransactionType TransactionType,
    DateTime? DueDate = null,
    IReadOnlySet<DateOnly>? BranchHolidays = null,
    Account? Account = null);

/// <summary>
/// Runs the shared pre-create checks and resolution used by transaction create, installment create,
/// and create preview flows. This service deliberately performs no persistence. It authenticates the
/// current branch user, resolves the role-dependent recorded-operator context, validates that every
/// referenced entity belongs to the branch, enforces Member account scope, applies lock-date rules,
/// and loads the holiday data needed for due-date calculation.
/// </summary>
/// <param name="authenticationService">Provides the authenticated branch user for the current request.</param>
/// <param name="memberAccountScopeResolver">Loads the caller's linked operator and active account scope in the branch.</param>
/// <param name="recordedByOperatorResolver">Applies role-based rules for the final recorded operator id.</param>
/// <param name="transactionBranchConsistencyService">Loads and validates the branch-scoped account, operator, client, and transaction type.</param>
/// <param name="memberAccountScopeGuard">Rejects Member callers that target an account outside their linked account scope.</param>
/// <param name="lockDateGuard">Rejects creates whose transaction date is on or before the branch lock date.</param>
/// <param name="branchHolidaySource">Provides branch holiday dates for due-date and installment calculations.</param>
public class TransactionCreatePreamble(
    IAuthenticationService authenticationService,
    IMemberAccountScopeResolver memberAccountScopeResolver,
    TransactionRecordedByOperatorResolver recordedByOperatorResolver,
    TransactionBranchConsistencyService transactionBranchConsistencyService,
    MemberAccountScopeGuard memberAccountScopeGuard,
    LockDateGuard lockDateGuard,
    IBranchHolidaySource branchHolidaySource)
{
    /// <summary>
    /// Resolves the complete pre-create context for a single transaction create or create preview.
    /// It runs the common branch/user/operator/account/type/client/scope checks, rejects cheque
    /// transaction types that omit an operator-entered due date, computes the final due date from the
    /// transaction type settlement rule and branch holidays, and verifies the transaction date is not
    /// locked. The returned context is ready for the create mapper or create-impact preview to use
    /// without performing additional branch-scope lookups.
    /// </summary>
    /// <param name="request">The create request body. Its ids are validated in branch scope and its due-date fields drive the final computed due date.</param>
    /// <returns>The resolved transaction create context, including due date, branch holidays, transaction type, account, branch user, and recorded operator id.</returns>
    /// <exception cref="OnValidationException">Thrown when the request violates create validation that depends on resolved context, such as a cheque type without a due date.</exception>
    /// <exception cref="ConflictException">Thrown when the transaction date is locked or the transaction type requires a Tab account and client but the request does not satisfy that invariant.</exception>
    /// <exception cref="NotFoundException">Thrown when the account, recorded operator, client, or transaction type is missing from the authenticated branch.</exception>
    /// <exception cref="TokenUnauthorizedException">Thrown by authentication when the request has no valid branch user context.</exception>
    /// <exception cref="TokenWithoutPermissionException">Thrown when a Member caller targets an account outside their linked account scope.</exception>
    public async Task<TransactionCreateContext> Resolve(RequestCreateTransactionJson request)
    {
        var context = await ResolveCommon(
            requestedRecordedByOperatorId: request.RecordedByOperatorId,
            accountId: request.AccountId,
            clientId: request.ClientId,
            transactionTypeId: request.TransactionTypeId);

        if (context.TransactionType.SettlementRule == SettlementRule.OperatorEnteredCheque && request.DueDate is null)
            throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_CHEQUE_REQUIRES_DUE_DATE]);

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

    /// <summary>
    /// Resolves the shared pre-create context for an installment transaction create or installment
    /// preview. It runs the same authentication, recorded-operator, branch-consistency, and Member
    /// account-scope checks as the single-create path, then verifies the base transaction date is not
    /// locked, and loads branch holidays for the installment plan builder. It does not compute a single
    /// due date because installment rows derive multiple due dates later from the installment request.
    /// </summary>
    /// <param name="request">The installment create request body. Its ids are validated in branch scope, and its date is checked against the lock date.</param>
    /// <returns>The resolved transaction create context, including branch holidays, transaction type, account, branch user, and recorded operator id.</returns>
    /// <exception cref="OnValidationException">Thrown when role-based recorded-operator resolution fails, such as a Member sending an override or lacking a linked operator.</exception>
    /// <exception cref="ConflictException">Thrown when the base transaction date is locked or branch consistency detects a fiado invariant conflict.</exception>
    /// <exception cref="NotFoundException">Thrown when the account, recorded operator, client, or transaction type is missing from the authenticated branch.</exception>
    /// <exception cref="TokenUnauthorizedException">Thrown by authentication when the request has no valid branch user context.</exception>
    /// <exception cref="TokenWithoutPermissionException">Thrown when a Member caller targets an account outside their linked account scope.</exception>
    public async Task<TransactionCreateContext> Resolve(RequestCreateTransactionInstallmentJson request)
    {
        var context = await ResolveCommon(
            requestedRecordedByOperatorId: request.RecordedByOperatorId,
            accountId: request.AccountId,
            clientId: request.ClientId,
            transactionTypeId: request.TransactionTypeId);

        await lockDateGuard.EnsureNotLocked(
            context.BranchUser.BranchId,
            request.Date,
            ResourcesErrorMessages.TRANSACTION_DATE_LOCKED);

        var holidayDates = await branchHolidaySource.GetHolidayDatesAsync(context.BranchUser.BranchId);

        return context with { BranchHolidays = holidayDates };
    }

    /// <summary>
    /// Performs the create-flow checks shared by single transaction, installment, and create-preview
    /// operations. The ordering is intentional: recorded-operator resolution happens before the Member
    /// account-scope guard, so a Member without a linked operator fails as a validation error from the
    /// recorded-operator resolver, while a linked Member targeting an unlinked account fails as a
    /// permission error from the scope guard. After this method returns, callers can trust that the
    /// referenced account, operator, optional client, and transaction type all belong to the current
    /// branch and that the transaction type is compatible with the account/client shape.
    /// </summary>
    /// <param name="requestedRecordedByOperatorId">The optional operator id supplied by the request. Members may not supply it; Manager/Admin callers may use it as an override.</param>
    /// <param name="accountId">The account id targeted by the transaction request.</param>
    /// <param name="clientId">The optional client id targeted by the transaction request.</param>
    /// <param name="transactionTypeId">The transaction type id selected by the request.</param>
    /// <returns>The resolved common create context without due-date or holiday enrichment.</returns>
    /// <exception cref="OnValidationException">Thrown by recorded-operator resolution for invalid role/operator combinations.</exception>
    /// <exception cref="ConflictException">Thrown when the transaction type requires a Tab account and client but the request does not satisfy that invariant.</exception>
    /// <exception cref="NotFoundException">Thrown when a branch-scoped account, operator, client, or transaction type cannot be found.</exception>
    /// <exception cref="TokenUnauthorizedException">Thrown by authentication when the request has no valid branch user context.</exception>
    /// <exception cref="TokenWithoutPermissionException">Thrown when a Member caller targets an account outside their linked account scope.</exception>
    private async Task<TransactionCreateContext> ResolveCommon(
        Guid? requestedRecordedByOperatorId,
        Guid accountId,
        Guid? clientId,
        Guid transactionTypeId)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var memberScope = await memberAccountScopeResolver.Resolve(branchUser.UserId, branchUser.BranchId);
        var callerOperator = memberScope.LinkedOperator;

        var recordedByOperatorId = recordedByOperatorResolver.Resolve(
            requestedRecordedByOperatorId,
            branchUser.Role,
            callerOperator);

        var consistency = await transactionBranchConsistencyService.ResolveAndValidate(
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
            TransactionType: consistency.TransactionType,
            Account: consistency.Account);
    }
}
