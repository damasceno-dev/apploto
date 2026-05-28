using NSubstitute;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Domain.Models;
using DailyLedgerListFilter = server.Domain.Models.DailyLedgerListFilter;

namespace CommonTestUtilities.Repositories;

public class TransactionsRepositoryBuilder
{
    private readonly ITransactionsRepository _repository = Substitute.For<ITransactionsRepository>();

    public TransactionsRepositoryBuilder GetByIdAndBranchIdReturns(
        Guid id,
        Guid branchId,
        Transaction? result)
    {
        _repository.GetByIdAndBranchId(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId))
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
                Arg.Is<Direction?>(value => value == direction))
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
