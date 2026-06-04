using server.Application.Services.Reports;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions.Update;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions.EditPreview;

/// <summary>
/// Non-persisting twin of <see cref="UpdateTransactionUseCase"/>. Mirrors the write chain step-for-step minus the mutation and Commit,
/// then projects the impact of the hypothetical edit. Does not inject <see cref="IUnitOfWork"/> (preview cannot commit by construction),
/// the member account-scope collaborators (preview is Manager/Admin only — the auth filter blocks Members),
/// or the branch-consistency service (the write twin does not use it either). The snapshot is loaded no-tracking and never mutated.
/// </summary>
public class PreviewEditTransactionUseCase(
    IAuthenticationService authenticationService,
    ITransactionsRepository transactionsRepository,
    IClientsRepository clientsRepository,
    ITransactionMutationPermissionGuard transactionMutationPermissionGuard,
    LockDateGuard lockDateGuard,
    TransactionEditImpactProjector editImpactProjector,
    IBranchClock branchClock)
{
    public async Task<ResponseEditTransactionPreviewJson> Execute(
        Guid transactionId,
        RequestUpdateTransactionJson request,
        DateTime? asOfDate = null,
        CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role != Role.Manager && branchUser.Role != Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        Validate(request);

        var snapshot = await transactionsRepository.GetByIdAndBranchIdAsNoTrackingWithTransactionType(transactionId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_NOT_FOUND);

        if (snapshot.Status == TransactionStatus.Cancelled)
        {
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_CANNOT_UPDATE_CANCELLED);
        }

        // Preview is Manager/Admin only, so there is never a linked caller operator to pass;
        // the mutation guard still enforces the same-day / status rules the write twin applies for Managers and Admins.
        var utcNow = DateTime.UtcNow;
        transactionMutationPermissionGuard.EnsureAllowed(
            snapshot,
            branchUser.Role,
            callerOperator: null,
            utcNow);

        await lockDateGuard.EnsureNotLocked(
            branchUser.BranchId,
            snapshot.Date,
            ResourcesErrorMessages.TRANSACTION_DATE_LOCKED,
            ct);

        if (snapshot.TransactionType.RequiresTabAccountAndClient && request.ClientId is null)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_FIADO_REQUIRES_CLIENT);

        if (request.ClientId is { } newClientId)
        {
            var client = await clientsRepository.GetActiveByIdAndBranchIdAsNoTracking(newClientId, branchUser.BranchId);
            if (client is null)
                throw new NotFoundException(ResourcesErrorMessages.CLIENT_NOT_FOUND);
        }

        UpdateTransactionEntityRelativeValidator.EnsureValid(snapshot, request);

        var edit = new HypotheticalTransactionEdit(snapshot, request);
        var resolvedAsOfDate = asOfDate ?? branchClock.LocalBusinessDate(branchClock.UtcNow());
        var impact = await editImpactProjector.Project(edit, resolvedAsOfDate, branchUser.BranchId, ct);

        return new ResponseEditTransactionPreviewJson
        {
            TransactionId = transactionId,
            Impact = impact,
            Warnings = []
        };
    }

    private static void Validate(RequestUpdateTransactionJson request)
    {
        var result = new UpdateTransactionFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(error => error.ErrorMessage).ToList());
        }
    }
}
