using FluentValidation;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.TransactionTypes.ReclassificationCandidates;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TransactionTypes.Reclassify;

/// <summary>
/// <c>POST /transaction-type/{id}/reclassify</c> (M9.7 Phase 1.3): the one explicit, audited way a posted row's frozen
/// classification changes. Manager/Admin names an open window (entirely after the lock date, not in the future), a
/// reason and the exact candidate rows with the versions they read; under the shared month boundary and the rows'
/// account keys (the settlement commands' coordination) the server re-reads
/// the candidates and requires the same set at the same versions, then writes one immutable revision per row (before
/// and after, reason, actor, batch) and the new snapshot in the same transaction. A locked month is never reclassified;
/// a stale or different set changes nothing. Applying the type's current markers is the only target, so there is no
/// free-form relabelling.
/// </summary>
public class ReclassifyTransactionTypeUseCase(
    IAuthenticationService authenticationService,
    ITransactionTypesRepository transactionTypesRepository,
    ITransactionClassificationRepository classificationRepository,
    ITransactionsRepository transactionsRepository,
    IDailyCloseAccountCoordination dailyCloseAccountCoordination,
    ILockDateReader lockDateReader,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseReclassificationJson> Execute(
        Guid transactionTypeId,
        RequestReclassifyTransactionTypeJson request,
        CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        if (branchUser.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var validation = new ReclassifyTransactionTypeFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).Distinct().ToList());

        var type = await transactionTypesRepository.GetByIdAndBranchIdAsNoTracking(transactionTypeId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);

        var dateFrom = request.DateFrom.Date;
        var dateTo = request.DateTo.Date;
        var today = branchClock.LocalBusinessDate(branchClock.UtcNow()).Date;
        if (dateTo > today)
            throw new OnValidationException([ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_WINDOW_IN_FUTURE]);

        if (dateFrom <= (await lockDateReader.Read(branchUser.BranchId, ct)).Date)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_LOCKED_PERIOD);

        // The same month boundary and account keys every settlement command takes (§6.24): a deposit, custody or
        // return on one of these rows either commits before the candidate read below — and the row is then no longer
        // a candidate for an instrument change — or waits until this reclassification commits and re-reads it.
        var accountIds = new List<Guid>();
        foreach (var row in request.Rows)
        {
            var key = await transactionsRepository.GetByIdAndBranchIdAsNoTracking(row.TransactionId, branchUser.BranchId, ct)
                ?? throw new ConflictException(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_STALE);
            accountIds.Add(key.AccountId);
        }
        await using var coordination = await dailyCloseAccountCoordination.AcquireMany(branchUser.BranchId, accountIds, ct);

        // Re-read inside the coordination: a month locked meanwhile refuses the whole window.
        var lockDate = await lockDateReader.Read(branchUser.BranchId, ct);
        if (dateFrom <= lockDate.Date)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_LOCKED_PERIOD);

        var candidates = await classificationRepository.ListReclassificationCandidatesAsNoTracking(
            type.Id, branchUser.BranchId, dateFrom, dateTo, ct);
        var requested = request.Rows.ToDictionary(row => row.TransactionId, row => row.Version);
        if (candidates.Count != requested.Count ||
            candidates.Any(row => requested.TryGetValue(row.Id, out var version) is false || version != row.Version))
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_STALE);

        var batchId = Guid.NewGuid();
        var utcNow = branchClock.UtcNow();
        var reason = request.Reason.Trim();
        var revisions = candidates
            .Select(row => new TransactionClassificationRevision
            {
                BranchId = branchUser.BranchId,
                TransactionId = row.Id,
                BatchId = batchId,
                Revision = row.ClassificationRevision + 1,
                PreviousSettlementInstrument = row.PostedSettlementInstrument,
                PreviousMonthlyFinancialRole = row.PostedMonthlyFinancialRole,
                PreviousFinancialClass = row.PostedFinancialClass,
                PreviousProvenance = row.ClassificationProvenance,
                SettlementInstrument = type.SettlementInstrument,
                MonthlyFinancialRole = type.MonthlyFinancialRole,
                FinancialClass = type.FinancialClass,
                Reason = reason,
                CreatedByUserId = branchUser.UserId
            })
            .ToList();

        await classificationRepository.AddRevisions(revisions, ct);
        await unitOfWork.Commit(ct);
        foreach (var (revision, row) in revisions.Zip(candidates))
        {
            if (await classificationRepository.TryApplyRevision(revision, row.Version, utcNow, ct) is false)
                throw new ConflictException(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_STALE);
        }

        var reclassified = await classificationRepository.ListReclassifiedRowsAsNoTracking(
            candidates.Select(row => row.Id).ToList(), branchUser.BranchId, ct);
        await coordination.Complete(ct);

        return ReclassificationMapper.ToResponse(type, dateFrom, dateTo, lockDate, reclassified, batchId);
    }
}

public class ReclassifyTransactionTypeFluentValidation : AbstractValidator<RequestReclassifyTransactionTypeJson>
{
    public ReclassifyTransactionTypeFluentValidation()
    {
        RuleFor(r => r.DateFrom).ReclassificationWindowStart(r => r.DateTo);
        RuleFor(r => r.Reason)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_REASON_REQUIRED)
            .MaximumLength(ReclassificationValidationExtensions.ReasonMaxLength)
            .WithMessage(string.Format(
                ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_REASON_MAX_LENGTH,
                ReclassificationValidationExtensions.ReasonMaxLength));
        RuleFor(r => r.Rows)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_ROWS_REQUIRED)
            .Must(rows => rows.Count <= ReclassificationValidationExtensions.MaxRows &&
                          rows.All(row => row.TransactionId != Guid.Empty && row.Version > 0) &&
                          rows.Select(row => row.TransactionId).Distinct().Count() == rows.Count)
            .WithMessage(ResourcesErrorMessages.TRANSACTION_TYPE_RECLASSIFY_ROWS_INVALID);
    }
}
