using server.Application.Services.Transactions;
using server.Application.Services.DailyCloses;
using server.Application.Services.Idempotency;
using server.Application.Services.Settings;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.CreateInstallment;

public class CreateTransactionInstallmentUseCase(
    IAuthenticationService authenticationService,
    TransactionCreatePreamble transactionCreatePreamble,
    InstallmentPlanBuilder installmentPlanBuilder,
    ITransactionsRepository transactionsRepository,
    IDailyCloseLedgerGuard dailyCloseLedgerGuard,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    FinancialCommandIdempotency financialCommandIdempotency,
    IBranchClock branchClock,
    LockDateGuard lockDateGuard,
    IUnitOfWork unitOfWork)
{
    private const string IdempotencyEndpoint = "POST /transaction/installment";

    public async Task<ResponseCreateTransactionInstallmentJson> Execute(
        RequestCreateTransactionInstallmentJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        Validate(request);
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var utcNow = branchClock.UtcNow();

        var replay = await financialCommandIdempotency
            .TryReplay<RequestCreateTransactionInstallmentJson, ResponseCreateTransactionInstallmentJson>(
                idempotencyKey,
                IdempotencyEndpoint,
                branchUser.BranchId,
                branchUser.UserId,
                request,
                utcNow,
                ct);
        if (replay is not null)
            return replay;

        await using var coordination = await dailyCloseAccountCoordination.Acquire(
            branchUser.BranchId,
            request.AccountId,
            ct);

        var idempotency = await financialCommandIdempotency
            .Prepare<RequestCreateTransactionInstallmentJson, ResponseCreateTransactionInstallmentJson>(
                idempotencyKey!,
                IdempotencyEndpoint,
                branchUser.BranchId,
                branchUser.UserId,
                request,
                utcNow,
                ct);
        if (idempotency.IsReplay)
        {
            await coordination.Complete(ct);
            return idempotency.ReplayResponse!;
        }

        var createContext = await transactionCreatePreamble.Resolve(request, ct);

        if (createContext.TransactionType.SettlementRule != SettlementRule.OperatorEnteredCheque)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_INSTALLMENT_REQUIRES_CHEQUE);

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
        var response = transactions.ToCreateInstallmentResponse();
        FinancialCommandIdempotency.Complete(idempotency, response.Installments[0].Id, response);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return response;
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
