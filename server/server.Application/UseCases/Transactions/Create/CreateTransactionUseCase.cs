using server.Application.Services.DailyCloses;
using server.Application.Services.Idempotency;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.Create;

public class CreateTransactionUseCase(
    IAuthenticationService authenticationService,
    TransactionCreatePreamble transactionCreatePreamble,
    ITransactionsRepository transactionsRepository,
    IDailyCloseLedgerGuard dailyCloseLedgerGuard,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    FinancialCommandIdempotency financialCommandIdempotency,
    IBranchClock branchClock,
    LockDateGuard lockDateGuard,
    FiadoPhysicalPairService fiadoPhysicalPairService,
    IAccountsRepository accountsRepository,
    IUnitOfWork unitOfWork)
{
    private const string IdempotencyEndpoint = "POST /transaction";

    public async Task<ResponseCreateTransactionJson> Execute(
        RequestCreateTransactionJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        Validate(request);
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var utcNow = branchClock.UtcNow();

        var replay = await financialCommandIdempotency
            .TryReplay<RequestCreateTransactionJson, ResponseCreateTransactionJson>(
                idempotencyKey,
                IdempotencyEndpoint,
                branchUser.BranchId,
                branchUser.UserId,
                request,
                utcNow,
                ct);
        if (replay is not null)
            return replay;

        // A counted-stock fiado sale also writes its paired terminal's ledger (M7.7 Phase 5.6): discover the pairing
        // without tracking, lock both accounts in the canonical order, and re-check the pairing inside the scope.
        var pairedTerminalId = request.HandsOutCountedStock
            ? await accountsRepository.GetActiveTerminalIdByTabAccountId(request.AccountId, branchUser.BranchId)
            : null;

        await using var coordination = pairedTerminalId is { } terminalToLock
            ? await dailyCloseAccountCoordination.AcquireMany(branchUser.BranchId, [request.AccountId, terminalToLock], ct)
            : await dailyCloseAccountCoordination.Acquire(branchUser.BranchId, request.AccountId, ct);

        var idempotency = await financialCommandIdempotency
            .Prepare<RequestCreateTransactionJson, ResponseCreateTransactionJson>(
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

        // The preamble check remains useful for fast failure and preview parity. Recheck while
        // holding the shared month-lock boundary so a successful lock command cannot be followed
        // by this write using a stale pre-acquisition LockDate.
        await lockDateGuard.EnsureNotLocked(
            createContext.BranchUser.BranchId,
            request.Date.Date,
            ResourcesErrorMessages.TRANSACTION_DATE_LOCKED,
            ct);

        var sessionContext = new SessionRowContext(
            createContext.BranchUser,
            createContext.MemberScope!,
            request.SessionId,
            request.HistoricalReason,
            request.OpeningAcknowledgment);
        TransactionSessionBinding.EnsureUsed(
            request.SessionId,
            request.OpeningAcknowledgment,
            createContext.Account!.Type == AccountType.Terminal || request.HandsOutCountedStock);
        var ownSession = await dailyCloseLedgerGuard.ResolveSessionForNewRow(
            request.AccountId,
            createContext.Account!.Type,
            request.Date.Date,
            sessionContext,
            ct);

        var transaction = request.ToTransaction(
            transactionType: createContext.TransactionType,
            recordedByOperatorId: createContext.RecordedByOperatorId,
            dueDate: createContext.DueDate!.Value,
            createdByUserId: createContext.BranchUser.UserId,
            branchId: createContext.BranchUser.BranchId);

        transaction.SessionId = ownSession.SessionId;
        Transaction? counterpart = null;
        SessionRowResolution? counterpartSession = null;
        if (request.HandsOutCountedStock)
        {
            (counterpart, counterpartSession) = await fiadoPhysicalPairService.BuildCounterpart(
                transaction,
                pairedTerminalId ?? throw new ConflictException(ResourcesErrorMessages.TRANSACTION_FIADO_PAIRED_TERMINAL_REQUIRED),
                TransactionGenerationKind.FiadoCountedStockSale,
                sessionContext,
                ct);
        }

        await transactionsRepository.Add(transaction, ct);
        if (counterpart is not null)
            await transactionsRepository.Add(counterpart, ct);
        await unitOfWork.Commit(ct);
        var response = transaction.ToCreateResponse();
        response.GeneratedTransactionId = counterpart?.Id;
        TransactionSessionBinding.Apply(response, ownSession, counterpartSession);
        FinancialCommandIdempotency.Complete(idempotency, transaction.Id, response);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return response;
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
