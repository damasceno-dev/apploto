using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.CreateInstallment;

public class CreateTransactionInstallmentUseCase(
    TransactionCreatePreamble transactionCreatePreamble,
    InstallmentPlanBuilder installmentPlanBuilder,
    ITransactionsRepository transactionsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateTransactionInstallmentJson> Execute(RequestCreateTransactionInstallmentJson request)
    {
        Validate(request);

        var createContext = await transactionCreatePreamble.Resolve(request);

        if (createContext.TransactionType.SettlementRule != SettlementRule.OperatorEnteredCheque)
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_REQUIRES_CHEQUE);
        }

        var installmentPlan = installmentPlanBuilder.Build(request, createContext.BranchUser.BranchId, Guid.NewGuid());
        var transactions = installmentPlan
            .Select(row => row.ToTransaction(
                request,
                createContext.TransactionType,
                createContext.RecordedByOperatorId,
                createContext.BranchUser.UserId))
            .ToList();

        await transactionsRepository.AddRange(transactions);
        await unitOfWork.Commit();

        return transactions.ToCreateInstallmentResponse();
    }

    private static void Validate(RequestCreateTransactionInstallmentJson request)
    {
        var result = new CreateTransactionInstallmentFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
