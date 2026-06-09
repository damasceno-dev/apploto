using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.Transactions;

/// <summary>
/// Result of branch-consistency resolution: the validated <see cref="TransactionType"/> (with its
/// <c>Category</c> loaded) plus the resolved <see cref="Account"/>. The account is surfaced so
/// callers can read its <c>Type</c> without a second load — the create-impact preview needs it to
/// build its hypothetical row.
/// </summary>
public sealed record TransactionBranchConsistencyResult(TransactionType TransactionType, Account Account);

public class TransactionBranchConsistencyService(IAccountsRepository accountsRepository, IOperatorsRepository operatorsRepository, IClientsRepository clientsRepository, ITransactionTypesRepository transactionTypesRepository)
{
    public async Task<TransactionBranchConsistencyResult> ResolveAndValidate(
        Guid branchId,
        Guid accountId,
        Guid recordedByOperatorId,
        Guid? clientId,
        Guid transactionTypeId)
    {
        var account = await accountsRepository.GetActiveByIdAndBranchIdAsNoTracking(accountId, branchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.ACCOUNT_NOT_FOUND);

        _ = await operatorsRepository.GetActiveByIdAndBranchIdAsNoTracking(recordedByOperatorId, branchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.OPERATOR_NOT_FOUND);

        if (clientId is { } resolvedClientId)
        {
            _ = await clientsRepository.GetActiveByIdAndBranchIdAsNoTracking(resolvedClientId, branchId)
                ?? throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        }

        var transactionType =
            await transactionTypesRepository.GetActiveByIdAndBranchIdWithCategoryAsNoTracking(transactionTypeId, branchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);

        if (transactionType.RequiresTabAccountAndClient &&
            (account.Type != AccountType.Tab || clientId.HasValue is false))
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_REQUIRES_TAB_ACCOUNT_AND_CLIENT);
        }

        return new TransactionBranchConsistencyResult(transactionType, account);
    }
}
