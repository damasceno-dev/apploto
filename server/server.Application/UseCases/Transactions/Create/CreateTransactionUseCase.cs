using server.Application.Services.Sessions;
using server.Application.Services.DailyCloses;
using server.Application.Services.Idempotency;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
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
    SessionRevisionService sessionRevisions,
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
        {
            await EnsureReplayAuthority(request.AccountId, replay, branchUser.BranchId, ct);
            return replay;
        }

        // Every new counter fiado sale also writes its paired cashbox's ledger (M9.6 Phase 6, §6.19 O05): discover the
        // pairing without tracking, lock both accounts in the canonical order, and re-check the pairing inside the scope.
        // A sale on an unpaired Tab is refused when the counterpart is built, before anything is written.
        var pairedTerminalId = await accountsRepository.GetActiveTerminalIdByTabAccountId(request.AccountId, branchUser.BranchId);

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
            await EnsureReplayAuthority(request.AccountId, idempotency.ReplayResponse!, branchUser.BranchId, ct);
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
        // The generic create is the counter: its fiado sales are counter sales, which always carry the cashbox leg.
        var isFiadoSale = TransactionCreatePreamble.IsCounterFiadoSale(createContext)
            && FinancialRoleRules.RequiresCashboxLeg(FiadoDebtOrigin.CounterSale);
        TransactionSessionBinding.EnsureUsed(
            request.SessionId,
            request.OpeningAcknowledgment,
            createContext.Account!.Type == AccountType.Terminal || isFiadoSale);
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
        if (isFiadoSale)
        {
            (counterpart, counterpartSession) = await fiadoPhysicalPairService.BuildCounterpart(
                transaction,
                pairedTerminalId ?? throw new ConflictException(ResourcesErrorMessages.TRANSACTION_FIADO_PAIRED_TERMINAL_REQUIRED),
                TransactionGenerationKind.FiadoSale,
                sessionContext,
                ct);
        }

        await transactionsRepository.Add(transaction, ct);
        if (counterpart is not null)
            await transactionsRepository.Add(counterpart, ct);
        await sessionRevisions.RecordLedgerChange(
            createContext.BranchUser.BranchId,
            new[] { transaction, counterpart }.OfType<Transaction>().Where(row => row.Status == TransactionStatus.Active),
            ct);
        await unitOfWork.Commit(ct);
        var response = transaction.ToCreateResponse();
        response.GeneratedTransactionId = counterpart?.Id;
        TransactionSessionBinding.Apply(response, ownSession, counterpartSession);
        FinancialCommandIdempotency.Complete(idempotency, transaction.Id, response);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return response;
    }

    private async Task EnsureReplayAuthority(Guid accountId, ResponseCreateTransactionJson replay, Guid branchId, CancellationToken ct)
    {
        Guid? counterpartAccountId = null;
        if (replay.GeneratedTransactionId is { } generatedId)
        {
            var counterpart = await transactionsRepository.GetByIdAndBranchIdAsNoTracking(generatedId, branchId, ct)
                ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);
            counterpartAccountId = counterpart.AccountId;
        }
        await transactionCreatePreamble.EnsureReplayAuthority(accountId, counterpartAccountId, ct);
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
