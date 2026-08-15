using server.Application.Services.DailyCloses;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.Create;

public class CreateTransactionUseCase(
    TransactionCreatePreamble transactionCreatePreamble,
    ITransactionsRepository transactionsRepository,
    IDailyCloseLedgerGuard dailyCloseLedgerGuard,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateTransactionJson> Execute(
        RequestCreateTransactionJson request,
        CancellationToken ct = default)
    {
        Validate(request);

        var createContext = await transactionCreatePreamble.Resolve(request, ct);

        await using var coordination = await dailyCloseAccountCoordination.Acquire(
            createContext.BranchUser.BranchId,
            request.AccountId,
            ct);

        await dailyCloseLedgerGuard.EnsureLedgerAcceptsNewRow(
            createContext.BranchUser.BranchId,
            request.AccountId,
            createContext.Account!.Type,
            request.Date.Date,
            ct);

        var transaction = request.ToTransaction(
            transactionType: createContext.TransactionType,
            recordedByOperatorId: createContext.RecordedByOperatorId,
            dueDate: createContext.DueDate!.Value,
            createdByUserId: createContext.BranchUser.UserId,
            branchId: createContext.BranchUser.BranchId);

        await transactionsRepository.Add(transaction, ct);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

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
