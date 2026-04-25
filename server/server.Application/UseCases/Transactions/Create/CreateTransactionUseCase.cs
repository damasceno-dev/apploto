using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.Create;

public class CreateTransactionUseCase(
    TransactionCreatePreamble transactionCreatePreamble,
    ITransactionsRepository transactionsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateTransactionJson> Execute(RequestCreateTransactionJson request)
    {
        Validate(request);

        var createContext = await transactionCreatePreamble.Resolve(request);

        var transaction = request.ToTransaction(
            transactionType: createContext.TransactionType,
            recordedByOperatorId: createContext.RecordedByOperatorId,
            dueDate: createContext.DueDate!.Value,
            createdByUserId: createContext.BranchUser.UserId,
            branchId: createContext.BranchUser.BranchId);

        await transactionsRepository.Add(transaction);
        await unitOfWork.Commit();

        return transaction.ToCreateResponse();
    }

    private static void Validate(RequestCreateTransactionJson request)
    {
        var result = new CreateTransactionFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
