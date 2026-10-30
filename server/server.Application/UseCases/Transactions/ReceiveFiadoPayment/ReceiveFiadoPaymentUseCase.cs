using server.Application.Services.DailyCloses;
using server.Application.Services.Idempotency;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.Create;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.ReceiveFiadoPayment;

/// <summary>
/// <c>POST /transaction/fiado-payment</c> — <i>Receber pagamento</i> (M7.7 Phase 5.1 command + 5.6). Writes the
/// Tab <c>In</c>; when the selected method is <see cref="FiadoPaymentKind.CountedCash"/> it also writes the generated
/// Terminal <c>In</c> on the Tab's paired terminal in the same commit, which needs that terminal's open Draft close
/// and, for a Member, the terminal in scope besides the explicit Tab grant. A non-cash method needs no close. The
/// persisted, endpoint/branch/user-scoped <c>Idempotency-Key</c> replays the original complete operation.
/// </summary>
public class ReceiveFiadoPaymentUseCase(
    IAuthenticationService authenticationService,
    TransactionCreatePreamble transactionCreatePreamble,
    ITransactionsRepository transactionsRepository,
    IAccountsRepository accountsRepository,
    ITransactionTypesRepository transactionTypesRepository,
    IDailyCloseLedgerGuard dailyCloseLedgerGuard,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    FinancialCommandIdempotency financialCommandIdempotency,
    FiadoPhysicalPairService fiadoPhysicalPairService,
    IBranchClock branchClock,
    LockDateGuard lockDateGuard,
    IUnitOfWork unitOfWork)
{
    public const string IdempotencyEndpoint = "POST /transaction/fiado-payment";

    public async Task<ResponseCreateTransactionJson> Execute(
        RequestReceiveFiadoPaymentJson request,
        string? idempotencyKey,
        CancellationToken ct = default)
    {
        Validate(request);
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var utcNow = branchClock.UtcNow();

        var replay = await financialCommandIdempotency
            .TryReplay<RequestReceiveFiadoPaymentJson, ResponseCreateTransactionJson>(
                idempotencyKey,
                IdempotencyEndpoint,
                branchUser.BranchId,
                branchUser.UserId,
                request,
                utcNow,
                ct);
        if (replay is not null)
            return replay;

        // Only a counted-cash method touches the paired terminal; lock it with the Tab when that is possible, and
        // re-check the method and the pairing inside the scope.
        var method = await transactionTypesRepository.GetActiveByIdAndBranchIdWithCategoryAsNoTracking(
            request.TransactionTypeId,
            branchUser.BranchId,
            ct);
        var pairedTerminalId = method?.FiadoPaymentKind == FiadoPaymentKind.CountedCash
            ? await accountsRepository.GetActiveTerminalIdByTabAccountId(request.AccountId, branchUser.BranchId)
            : null;

        await using var coordination = pairedTerminalId is { } terminalToLock
            ? await dailyCloseAccountCoordination.AcquireMany(branchUser.BranchId, [request.AccountId, terminalToLock], ct)
            : await dailyCloseAccountCoordination.Acquire(branchUser.BranchId, request.AccountId, ct);

        var idempotency = await financialCommandIdempotency
            .Prepare<RequestReceiveFiadoPaymentJson, ResponseCreateTransactionJson>(
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

        var createRequest = request.ToCreateRequest();
        var context = await transactionCreatePreamble.ResolveFiadoPayment(createRequest, ct);

        await lockDateGuard.EnsureNotLocked(
            context.BranchUser.BranchId,
            request.Date.Date,
            ResourcesErrorMessages.TRANSACTION_DATE_LOCKED,
            ct);

        var sessionContext = new SessionRowContext(
            context.BranchUser,
            context.MemberScope!,
            request.SessionId,
            request.HistoricalReason,
            request.OpeningAcknowledgment);
        var ownSession = await dailyCloseLedgerGuard.ResolveSessionForNewRow(
            request.AccountId,
            context.Account!.Type,
            request.Date.Date,
            sessionContext,
            ct);

        var payment = createRequest.ToTransaction(
            transactionType: context.TransactionType,
            recordedByOperatorId: context.RecordedByOperatorId,
            dueDate: context.DueDate!.Value,
            createdByUserId: context.BranchUser.UserId,
            branchId: context.BranchUser.BranchId);

        payment.SessionId = ownSession.SessionId;
        Transaction? counterpart = null;
        SessionRowResolution? counterpartSession = null;
        var writesCashboxRow = ownSession.Session is not null
            || context.TransactionType.FiadoPaymentKind == FiadoPaymentKind.CountedCash;
        TransactionSessionBinding.EnsureUsed(request.SessionId, request.OpeningAcknowledgment, writesCashboxRow);
        if (context.TransactionType.FiadoPaymentKind == FiadoPaymentKind.CountedCash)
        {
            (counterpart, counterpartSession) = await fiadoPhysicalPairService.BuildCounterpart(
                payment,
                pairedTerminalId ?? throw new ConflictException(ResourcesErrorMessages.TRANSACTION_FIADO_PAIRED_TERMINAL_REQUIRED),
                TransactionGenerationKind.FiadoCashRepayment,
                sessionContext,
                ct);
        }

        await transactionsRepository.Add(payment, ct);
        if (counterpart is not null)
            await transactionsRepository.Add(counterpart, ct);
        await unitOfWork.Commit(ct);
        var response = payment.ToCreateResponse();
        response.GeneratedTransactionId = counterpart?.Id;
        TransactionSessionBinding.Apply(response, ownSession, counterpartSession);
        FinancialCommandIdempotency.Complete(idempotency, payment.Id, response);
        await unitOfWork.Commit(ct);
        await coordination.Complete(ct);

        return response;
    }

    private static void Validate(RequestReceiveFiadoPaymentJson request)
    {
        var result = new ReceiveFiadoPaymentFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
    }
}

internal static class ReceiveFiadoPaymentMapper
{
    public static RequestCreateTransactionJson ToCreateRequest(this RequestReceiveFiadoPaymentJson request)
    {
        return new RequestCreateTransactionJson
        {
            Date = request.Date,
            Value = request.Value,
            TransactionTime = request.TransactionTime,
            Description = request.Description,
            TransactionTypeId = request.TransactionTypeId,
            AccountId = request.AccountId,
            ClientId = request.ClientId,
            RecordedByOperatorId = request.RecordedByOperatorId,
            SaveAsDraft = request.SaveAsDraft,
            SessionId = request.SessionId,
            HistoricalReason = request.HistoricalReason,
            OpeningAcknowledgment = request.OpeningAcknowledgment
        };
    }
}
