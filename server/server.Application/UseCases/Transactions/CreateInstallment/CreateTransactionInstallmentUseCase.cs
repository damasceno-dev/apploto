using server.Application.Services.Transactions;
using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
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
    IDailyCloseLedgerGuard dailyCloseLedgerGuard,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    LockDateGuard lockDateGuard,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateTransactionInstallmentJson> Execute(
        RequestCreateTransactionInstallmentJson request,
        CancellationToken ct = default)
    {
        Validate(request);

        var createContext = await transactionCreatePreamble.Resolve(request, ct);

        if (createContext.TransactionType.SettlementRule != SettlementRule.OperatorEnteredCheque)
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_REQUIRES_CHEQUE);
        }

        await using var coordination = await dailyCloseAccountCoordination.Acquire(
            createContext.BranchUser.BranchId,
            request.AccountId,
            ct);

        await lockDateGuard.EnsureNotLocked(
            createContext.BranchUser.BranchId,
            request.Date.Date,
            ResourcesErrorMessages.TRANSACTION_DATE_LOCKED,
            ct);

        await dailyCloseLedgerGuard.EnsureLedgerAcceptsNewRow(
            createContext.BranchUser.BranchId,
            request.AccountId,
            createContext.Account!.Type,
            request.Date.Date,
            ct);

        var installmentPlan = installmentPlanBuilder.Build(
            request,
            createContext.BranchUser.BranchId,
            Guid.NewGuid(),
            createContext.BranchHolidays);
        var transactions = installmentPlan
            .Select(row => row.ToTransaction(
                request,
                createContext.TransactionType,
                createContext.RecordedByOperatorId,
                createContext.BranchUser.UserId))
            .ToList();

        await transactionsRepository.AddRange(transactions, ct);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

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
