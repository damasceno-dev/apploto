using server.Application.Services.DailyCloses;
using server.Application.Services.Reports;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Reports.FinancialResult;

public class GetFinancialResultUseCase(
    IAuthenticationService authenticationService,
    IFinancialResultRepository repository,
    ICashVarianceProductResolver varianceProduct,
    IReportReadSnapshot snapshot,
    IBranchClock clock)
{
    public async Task<ResponseFinancialResultJson> Execute(RequestFinancialResultJson request, CancellationToken ct = default)
    {
        var actor = await authenticationService.GetAuthenticatedBranchUser();
        if (actor.Role is not Role.Manager and not Role.Admin)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        var validation = new FinancialResultFluentValidation().Validate(request);
        if (!validation.IsValid)
            throw new OnValidationException(validation.Errors.Select(e => e.ErrorMessage).ToList());
        var today = clock.LocalBusinessDate(clock.UtcNow());
        var cutoff = request.AsOfDate ?? today;
        if (cutoff > today || MonthlyFinancialReader.IsFutureMonth(request.Year, request.Month, cutoff))
            throw new OnValidationException([ResourcesErrorMessages.REPORT_MONTH_IN_FUTURE]);
        var (from, to, partial) = MonthlyFinancialReader.MonthWindow(request.Year, request.Month, cutoff);
        await using var read = await snapshot.Begin(ct);
        var productId = await varianceProduct.GetIdAsync(actor.BranchId, ct);
        var result = await repository.Read(actor.BranchId, productId, from, to, request.FinancialClass, request.Page, request.PageSize, ct);
        return new ResponseFinancialResultJson
        {
            DateFrom = from, DateTo = to, AsOfDate = cutoff, IsPartialMonth = partial,
            KnownOwnResult = result.Totals.Where(t => t.FinancialClass is FinancialClass.ShopFeesAndIncome or FinancialClass.OperatingExpenseAndLoss)
                .Sum(t => t.KnownSignedTotal),
            ClassificationComplete = result.ClassificationComplete,
            FeeAmountsComplete = result.UnresolvedFeeCount == 0, BankPositionComplete = false,
            TotalCount = result.TotalCount, Page = request.Page, PageSize = request.PageSize,
            Classes = result.Totals.OrderBy(t => t.FinancialClass).Select(t => new ResponseFinancialClassTotalJson
            {
                FinancialClass = t.FinancialClass, KnownSignedTotal = t.KnownSignedTotal, KnownVolume = t.KnownVolume,
                Count = t.Count, UnknownAmountCount = t.UnknownAmountCount, MigratedCount = t.MigratedCount
            }).ToList(),
            Entries = result.Entries.Select(e => new ResponseFinancialResultEntryJson
            {
                AccountName = e.AccountName, AccountActive = e.AccountActive, OfficialAttemptId = e.OfficialAttemptId,
                SourceHistoryComplete = e.SourceHistoryComplete,
                TransactionId = e.TransactionId, BankReceiptId = e.BankReceiptId,
                SourceId = e.SourceId, SourceKind = e.SourceKind, AccountId = e.AccountId, SessionId = e.SessionId,
                SourceDate = e.SourceDate, PostedOn = e.PostedOn, FinancialClass = e.FinancialClass,
                SnapshotFinancialClass = e.SnapshotFinancialClass, ClassificationConflict = e.ClassificationConflict,
                Provenance = e.Provenance, SignedAmount = e.SignedAmount
            }).ToList()
        };
    }
}
