using FluentValidation;
using server.Application.Services.Settings;
using server.Application.Services.Transactions;
using server.Application.UseCases.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.TransactionTypes.ReclassificationCandidates;

/// <summary>
/// <c>GET /transaction-type/{id}/reclassification-candidates</c> (M9.7 Phase 1.3): the posted rows of the type dated in
/// the window — never on or before the lock date — whose frozen classification differs from the type's current
/// markers, with their versions. The window is clamped to the open period; this read never changes anything.
/// </summary>
public class GetReclassificationCandidatesUseCase(
    IAuthenticationService authenticationService,
    ITransactionTypesRepository transactionTypesRepository,
    ITransactionClassificationRepository classificationRepository,
    ILockDateReader lockDateReader,
    IBranchClock branchClock)
{
    public async Task<ResponseReclassificationJson> Execute(
        Guid transactionTypeId,
        RequestReclassificationCandidatesJson request,
        CancellationToken ct = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        if (branchUser.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var validation = new ReclassificationCandidatesFluentValidation().Validate(request);
        if (validation.IsValid is false)
            throw new OnValidationException(validation.Errors.Select(error => error.ErrorMessage).Distinct().ToList());

        var type = await transactionTypesRepository.GetByIdAndBranchIdAsNoTracking(transactionTypeId, branchUser.BranchId, ct)
            ?? throw new NotFoundException(ResourcesErrorMessages.TRANSACTION_TYPE_NOT_FOUND);

        var lockDate = await lockDateReader.Read(branchUser.BranchId, ct);
        var today = branchClock.LocalBusinessDate(branchClock.UtcNow()).Date;
        var from = request.DateFrom.Date > lockDate.Date ? request.DateFrom.Date : lockDate.Date.AddDays(1);
        var to = request.DateTo.Date < today ? request.DateTo.Date : today;
        var rows = from > to
            ? []
            : await classificationRepository.ListReclassificationCandidatesAsNoTracking(type.Id, branchUser.BranchId, from, to, ct);

        return ReclassificationMapper.ToResponse(type, request.DateFrom.Date, request.DateTo.Date, lockDate, rows, batchId: null);
    }
}

public class ReclassificationCandidatesFluentValidation : AbstractValidator<RequestReclassificationCandidatesJson>
{
    public ReclassificationCandidatesFluentValidation()
    {
        RuleFor(r => r.DateFrom).ReclassificationWindowStart(r => r.DateTo);
    }
}

internal static class ReclassificationMapper
{
    public static ResponseReclassificationJson ToResponse(
        TransactionType type,
        DateTime dateFrom,
        DateTime dateTo,
        DateTime lockDate,
        IReadOnlyList<Transaction> rows,
        Guid? batchId) => new()
    {
        TransactionTypeId = type.Id,
        DateFrom = dateFrom,
        DateTo = dateTo,
        LockDate = lockDate == DateTime.MinValue ? null : lockDate.Date,
        TargetSettlementInstrument = type.SettlementInstrument,
        TargetMonthlyFinancialRole = type.MonthlyFinancialRole,
        TargetFinancialClass = type.FinancialClass,
        BatchId = batchId,
        Rows = rows
            .Select(row => new ResponseReclassificationRowJson
            {
                TransactionId = row.Id,
                Date = row.Date,
                Value = row.Value,
                Direction = row.Direction,
                Version = row.Version,
                Classification = row.ToClassificationResponse()
            })
            .ToList()
    };
}
