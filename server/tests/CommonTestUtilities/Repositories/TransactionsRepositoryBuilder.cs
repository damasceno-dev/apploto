using NSubstitute;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Domain.Models.Projections;
using DailyLedgerListFilter = server.Domain.Models.DailyLedgerListFilter;
using OpenChequeGroupRow = server.Domain.Models.Projections.OpenChequeGroupRow;
using AccountType = server.Domain.Entities.Enums.AccountType;

namespace CommonTestUtilities.Repositories;

public class TransactionsRepositoryBuilder
{
    private readonly ITransactionsRepository _repository = Substitute.For<ITransactionsRepository>();

    public TransactionsRepositoryBuilder()
    {
        // Whole-set report totals default to an empty set, so report tests that do not care about them still run.
        _repository
            .GetFiadoAgingTotalsByBranchIdAsNoTracking(default, default, default, default, default)
            .ReturnsForAnyArgs(new FiadoAgingTotals([], null));
        _repository
            .ListOpenChequeTotalsByDueDateAsNoTracking(default, default, default, default)
            .ReturnsForAnyArgs([]);
    }

    public TransactionsRepositoryBuilder GetFiadoAgingTotalsByBranchIdAsNoTrackingReturns(
        Guid branchId,
        Guid? accountId,
        Guid? clientId,
        DateTime asOfDate,
        FiadoAgingTotals result)
    {
        _repository.GetFiadoAgingTotalsByBranchIdAsNoTracking(branchId, accountId, clientId, asOfDate, Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder ListOpenChequeTotalsByDueDateAsNoTrackingReturns(
        Guid branchId,
        Guid? accountId,
        Guid? clientId,
        IReadOnlyList<AgingDueDateTotalRow> result)
    {
        _repository.ListOpenChequeTotalsByDueDateAsNoTracking(branchId, accountId, clientId, Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder GetByIdAndBranchIdReturns(
        Guid id,
        Guid branchId,
        Transaction? result)
    {
        _repository.GetByIdAndBranchId(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        _repository.GetByIdAndBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder GetByIdAndBranchIdAsNoTrackingReturns(
        Guid id,
        Guid branchId,
        Transaction? result)
    {
        _repository.GetByIdAndBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder GetByIdAndBranchIdAsNoTrackingWithTransactionTypeReturns(
        Guid id,
        Guid branchId,
        Transaction? result)
    {
        _repository.GetByIdAndBranchIdAsNoTrackingWithTransactionType(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder ListByBranchIdAsNoTrackingReturns(
        Guid branchId,
        TransactionListFilter filter,
        IReadOnlyList<Transaction> result)
    {
        _repository.ListByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<TransactionListFilter>(actual => MatchesFilter(filter, actual)))
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder CountByBranchIdAsNoTrackingReturns(
        Guid branchId,
        TransactionListFilter filter,
        int count)
    {
        _repository.CountByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<TransactionListFilter>(actual => MatchesFilter(filter, actual)))
            .Returns(count);
        return this;
    }

    public TransactionsRepositoryBuilder SumActiveValueByAccountAndDateAsNoTrackingReturns(
        Guid branchId,
        Guid accountId,
        DateTime date,
        Direction? direction,
        decimal result)
    {
        _repository.SumActiveValueByAccountAndDateAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid>(value => value == accountId),
                Arg.Is<DateTime>(value => value == date),
                Arg.Is<Direction?>(value => value == direction),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder ListByBranchIdAndAccountIdAndDateRangeAsNoTrackingReturns(
        Guid branchId,
        DailyLedgerListFilter filter,
        IReadOnlyList<Transaction> result)
    {
        _repository.ListByBranchIdAndAccountIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DailyLedgerListFilter>(actual => MatchesDailyLedgerFilter(filter, actual)))
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder CountByBranchIdAndAccountIdAndDateRangeAsNoTrackingReturns(
        Guid branchId,
        DailyLedgerListFilter filter,
        int count)
    {
        _repository.CountByBranchIdAndAccountIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DailyLedgerListFilter>(actual => MatchesDailyLedgerFilter(filter, actual)))
            .Returns(count);
        return this;
    }

    public TransactionsRepositoryBuilder SumActiveByAccountAndDateBeforeAsNoTrackingReturns(
        Guid branchId,
        Guid accountId,
        DateTime dateExclusive,
        decimal result)
    {
        _repository.SumActiveByAccountAndDateBeforeAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid>(value => value == accountId),
                Arg.Is<DateTime>(value => value == dateExclusive))
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder SumActiveByAccountAndDateRangeAsNoTrackingReturns(
        Guid branchId,
        Guid accountId,
        DateTime dateFrom,
        DateTime dateTo,
        decimal totalIn,
        decimal totalOut)
    {
        _repository.SumActiveByAccountAndDateRangeAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid>(value => value == accountId),
                Arg.Is<DateTime>(value => value == dateFrom),
                Arg.Is<DateTime>(value => value == dateTo))
            .Returns((totalIn, totalOut));
        return this;
    }

    public TransactionsRepositoryBuilder ListByOriginTransactionIdAndBranchIdAsNoTrackingReturns(
        Guid originId,
        Guid branchId,
        IReadOnlyList<Transaction> result)
    {
        _repository.ListByOriginTransactionIdAndBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == originId),
                Arg.Is<Guid>(value => value == branchId))
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder ListFiadoBalancesByBranchIdAsNoTrackingReturns(
        Guid branchId,
        Guid? clientId,
        DateTime asOfDate,
        IReadOnlyList<FiadoClientBalanceRow> result)
    {
        _repository.ListFiadoBalancesByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid?>(value => value == clientId),
                Arg.Is<DateTime>(value => value == asOfDate),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder ListFiadoOpenSalesByBranchIdAsNoTrackingReturns(
        Guid branchId,
        Guid? accountId,
        Guid? clientId,
        DateTime asOfDate,
        int page,
        int pageSize,
        IReadOnlyList<FiadoOpenSaleRow> result)
    {
        _repository.ListFiadoOpenSalesByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid?>(value => value == accountId),
                Arg.Is<Guid?>(value => value == clientId),
                Arg.Is<DateTime>(value => value == asOfDate),
                Arg.Is<int>(value => value == page),
                Arg.Is<int>(value => value == pageSize),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder CountFiadoOpenSalesByBranchIdAsNoTrackingReturns(
        Guid branchId,
        Guid? accountId,
        Guid? clientId,
        DateTime asOfDate,
        int count)
    {
        _repository.CountFiadoOpenSalesByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid?>(value => value == accountId),
                Arg.Is<Guid?>(value => value == clientId),
                Arg.Is<DateTime>(value => value == asOfDate),
                Arg.Any<CancellationToken>())
            .Returns(count);
        return this;
    }

    public TransactionsRepositoryBuilder GetFiadoClientPositionAsNoTrackingReturns(
        Guid branchId,
        Guid clientId,
        DateTime asOfDate,
        FiadoClientPosition result)
    {
        _repository.GetFiadoClientPositionAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid>(value => value == clientId),
                Arg.Is<DateTime>(value => value == asOfDate),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder SumByBranchIdAndOperatorIdAndDateRangeAsNoTrackingReturns(
        Guid branchId,
        Guid operatorId,
        DateTime dateFrom,
        DateTime dateTo,
        IReadOnlyList<Guid>? allowedAccountIds,
        OperatorTransactionSummaryProjection result)
    {
        _repository.SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid>(value => value == operatorId),
                Arg.Is<DateTime>(value => value == dateFrom),
                Arg.Is<DateTime>(value => value == dateTo),
                Arg.Is<IReadOnlyList<Guid>?>(actual => MatchesAllowedAccountIds(allowedAccountIds, actual)))
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder ListOpenChequeGroupsByBranchIdAsNoTrackingReturns(
        Guid branchId,
        Guid? accountId,
        Guid? clientId,
        int page,
        int pageSize,
        IReadOnlyList<OpenChequeGroupRow> result)
    {
        _repository.ListOpenChequeGroupsByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid?>(value => value == accountId),
                Arg.Is<Guid?>(value => value == clientId),
                Arg.Is<int>(value => value == page),
                Arg.Is<int>(value => value == pageSize),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder CountOpenChequeGroupsByBranchIdAsNoTrackingReturns(
        Guid branchId,
        Guid? accountId,
        Guid? clientId,
        int count)
    {
        _repository.CountOpenChequeGroupsByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid?>(value => value == accountId),
                Arg.Is<Guid?>(value => value == clientId),
                Arg.Any<CancellationToken>())
            .Returns(count);
        return this;
    }

    public TransactionsRepositoryBuilder CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTrackingReturns(
        Guid branchId,
        int year,
        int month,
        IReadOnlyList<MonthlyTransactionCountRow> result)
    {
        _repository.CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<int>(value => value == year),
                Arg.Is<int>(value => value == month),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder CountDraftByBranchIdAndDateRangeAsNoTrackingReturns(
        Guid branchId,
        DateTime afterDateExclusive,
        DateTime throughDateInclusive,
        int result)
    {
        _repository.CountDraftByBranchIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DateTime>(value => value == afterDateExclusive),
                Arg.Is<DateTime>(value => value == throughDateInclusive),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder GetEarliestDateByBranchIdAsNoTrackingReturns(Guid branchId, DateTime? result)
    {
        _repository.GetEarliestDateByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTrackingReturns(
        Guid branchId,
        int year,
        int month,
        IReadOnlyList<TerminalActivityPairRow> result)
    {
        _repository.ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<int>(value => value == year),
                Arg.Is<int>(value => value == month),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder ListUpcomingSettlementTotalsByBranchIdAsNoTrackingReturns(
        Guid branchId,
        UpcomingSettlementFilter filter,
        IReadOnlyList<UpcomingSettlementInstrumentTotalRow> result)
    {
        _repository.ListUpcomingSettlementTotalsByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<UpcomingSettlementFilter>(value => value == filter),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder ListUpcomingSettlementsByBranchIdAsNoTrackingReturns(
        Guid branchId,
        UpcomingSettlementFilter filter,
        int page,
        int pageSize,
        IReadOnlyList<UpcomingSettlementRow> result)
    {
        _repository.ListUpcomingSettlementsByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<UpcomingSettlementFilter>(value => value == filter),
                Arg.Is<int>(value => value == page),
                Arg.Is<int>(value => value == pageSize),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder ListMonthlyFinancialRoleRowsByBranchIdAsNoTrackingReturns(
        Guid branchId,
        MonthlyFinancialRole role,
        DateTime dateFrom,
        DateTime dateTo,
        IReadOnlyList<MonthlyFinancialRoleRow> result)
    {
        _repository.ListMonthlyFinancialRoleRowsByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<MonthlyFinancialRole>(value => value == role),
                Arg.Is<DateTime>(value => value == dateFrom),
                Arg.Is<DateTime>(value => value == dateTo),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public TransactionsRepositoryBuilder GetMonthlyFinancialRoleTotalAsNoTrackingReturns(
        Guid branchId,
        MonthlyFinancialRole role,
        DateTime dateFrom,
        DateTime dateTo,
        MonthlyFinancialRoleTotal result)
    {
        _repository.GetMonthlyFinancialRoleTotalAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<MonthlyFinancialRole>(value => value == role),
                Arg.Is<DateTime>(value => value == dateFrom),
                Arg.Is<DateTime>(value => value == dateTo),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public ITransactionsRepository Build()
    {
        return _repository;
    }

    private static bool MatchesFilter(TransactionListFilter expected, TransactionListFilter actual)
    {
        return expected.AccountId == actual.AccountId &&
               expected.DateFrom == actual.DateFrom &&
               expected.DateTo == actual.DateTo &&
               expected.Status == actual.Status &&
               expected.OperatorId == actual.OperatorId &&
               expected.ClientId == actual.ClientId &&
               MatchesAllowedAccountIds(expected.AllowedAccountIds, actual.AllowedAccountIds) &&
               expected.Page == actual.Page &&
               expected.PageSize == actual.PageSize;
    }

    private static bool MatchesAllowedAccountIds(IReadOnlyList<Guid>? expected, IReadOnlyList<Guid>? actual)
    {
        return expected is null
            ? actual is null
            : actual is not null && expected.SequenceEqual(actual);
    }

    private static bool MatchesDailyLedgerFilter(DailyLedgerListFilter expected, DailyLedgerListFilter actual)
    {
        return expected.AccountId == actual.AccountId &&
               expected.DateFrom == actual.DateFrom &&
               expected.DateTo == actual.DateTo &&
               expected.Page == actual.Page &&
               expected.PageSize == actual.PageSize;
    }
}
