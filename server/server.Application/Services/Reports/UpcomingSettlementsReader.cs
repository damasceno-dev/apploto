using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace server.Application.Services.Reports;

/// <summary>
/// The one upcoming-settlements computation (M11 Phase 5), shared by the full report and the management overview so a
/// predicate change cannot leave either behind. Since M9.7 Phase 1 the instrument is the row's frozen snapshot and the
/// amounts are what is still outstanding at <c>asOfDate</c> on the posted basis. No authorization or validation: callers
/// own both.
/// </summary>
public class UpcomingSettlementsReader(
    ITransactionsRepository transactionsRepository,
    ITransactionTypesRepository transactionTypesRepository)
{
    private static readonly SettlementInstrument[] ClassifiedInstruments =
        Enum.GetValues<SettlementInstrument>().Where(instrument => instrument != SettlementInstrument.None).ToArray();

    public async Task<ResponseUpcomingSettlementsJson> Read(
        Guid branchId,
        DateTime asOfDate,
        DateTime dueDateFrom,
        DateTime dueDateTo,
        Guid? accountId,
        SettlementInstrument? instrument,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        // Configured means a type carries an instrument today or a row was posted with one (a cleared type keeps its
        // posted rows reported, M9.7 Phase 1.3).
        var isConfigured = await transactionTypesRepository.ExistsWithSettlementInstrumentByBranchIdAsNoTracking(branchId, ct)
            || await transactionsRepository.ExistsPostedWithSettlementInstrumentByBranchIdAsNoTracking(branchId, ct);

        var window = new UpcomingSettlementFilter(dueDateFrom.Date, dueDateTo.Date, accountId, instrument, asOfDate.Date);
        var overdue = new UpcomingSettlementFilter(null, asOfDate.Date.AddDays(-1), accountId, instrument, asOfDate.Date);

        var windowTotals = await transactionsRepository.ListUpcomingSettlementTotalsByBranchIdAsNoTracking(branchId, window, ct);
        var overdueTotals = await transactionsRepository.ListUpcomingSettlementTotalsByBranchIdAsNoTracking(branchId, overdue, ct);
        var items = await transactionsRepository.ListUpcomingSettlementsByBranchIdAsNoTracking(branchId, window, page, pageSize, ct);

        var totalCount = windowTotals.Sum(row => row.Count);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

        return new ResponseUpcomingSettlementsJson
        {
            AsOfDate = asOfDate.Date,
            IsConfigured = isConfigured,
            DueDateFrom = dueDateFrom.Date,
            DueDateTo = dueDateTo.Date,
            TotalCount = totalCount,
            TotalValue = windowTotals.Sum(row => row.Value),
            ByInstrument = ClassifiedInstruments
                .Select(classified =>
                {
                    var row = windowTotals.SingleOrDefault(total => total.Instrument == classified);
                    return new ResponseSettlementInstrumentTotalJson
                    {
                        Instrument = classified,
                        Count = row?.Count ?? 0,
                        Value = row?.Value ?? 0m
                    };
                })
                .ToList(),
            OverdueCount = overdueTotals.Sum(row => row.Count),
            OverdueValue = overdueTotals.Sum(row => row.Value),
            Items = items
                .Select(row => new ResponseUpcomingSettlementItemJson
                {
                    TransactionId = row.TransactionId,
                    OriginTransactionId = row.OriginTransactionId,
                    Date = row.Date,
                    DueDate = row.DueDate,
                    Value = row.Value,
                    OutstandingValue = row.OutstandingValue,
                    Instrument = row.Instrument,
                    TransactionTypeId = row.TransactionTypeId,
                    TransactionTypeName = row.TransactionTypeName,
                    AccountId = row.AccountId,
                    AccountName = row.AccountName,
                    ClientId = row.ClientId,
                    ClientName = row.ClientName
                })
                .ToList(),
            TotalPages = totalPages,
            HasNext = page < totalPages,
            HasPrevious = page > 1
        };
    }
}
