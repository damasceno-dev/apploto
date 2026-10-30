using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;
using server.Domain.Models.Projections;

namespace CommonTestUtilities.Repositories;

public class DailyClosesRepositoryBuilder
{
    private readonly IDailyClosesRepository _repository = Substitute.For<IDailyClosesRepository>();

    public DailyClosesRepositoryBuilder()
    {
        _repository
            .ListManagerQueueClosesByBranchIdAndDateAsNoTracking(
                Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new List<ManagerQueueCloseRow>());
        _repository
            .GetManagerQueueBacklogByBranchIdAndDateRangeAsNoTracking(
                Arg.Any<Guid>(), Arg.Any<DateTime>(), Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(new ManagerQueueBacklogRow(0, 0, 0, null));
    }

    public DailyClosesRepositoryBuilder GetManagerQueueBacklogByBranchIdAndDateRangeAsNoTrackingReturns(
        Guid branchId,
        DateTime afterDateExclusive,
        DateTime beforeDateExclusive,
        ManagerQueueBacklogRow result)
    {
        _repository.GetManagerQueueBacklogByBranchIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DateTime>(value => value == afterDateExclusive),
                Arg.Is<DateTime>(value => value == beforeDateExclusive),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder ListManagerQueueClosesByBranchIdAndDateAsNoTrackingReturns(
        Guid branchId,
        DateTime date,
        IReadOnlyList<ManagerQueueCloseRow> result)
    {
        _repository.ListManagerQueueClosesByBranchIdAndDateAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DateTime>(value => value == date),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder GetByIdAndBranchIdReturns(
        Guid id,
        Guid branchId,
        DailyClose? result)
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

    public DailyClosesRepositoryBuilder GetByIdAndBranchIdAsNoTrackingReturns(
        Guid id,
        Guid branchId,
        DailyClose? result)
    {
        _repository.GetByIdAndBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == id),
                Arg.Is<Guid>(value => value == branchId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder GetActiveCustodyByAccountIdAsNoTrackingReturns(
        Guid branchId,
        Guid accountId,
        DailyClose? result)
    {
        _repository.GetActiveCustodyByAccountIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<Guid>(value => value == accountId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder GetCountedPredecessorAsNoTrackingReturns(Guid sessionId, DailyClose? result)
    {
        _repository.GetCountedPredecessorAsNoTracking(
                Arg.Is<DailyClose>(session => session.Id == sessionId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder GetCountedPredecessorAsNoTrackingReturnsForAny(DailyClose? result)
    {
        _repository.GetCountedPredecessorAsNoTracking(Arg.Any<DailyClose>(), Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder GetDependentSuccessorReturnsForAny(DailyClose? result)
    {
        _repository.GetDependentSuccessor(Arg.Any<DailyClose>(), Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder GetDependentSuccessorReturns(Guid sessionId, DailyClose? result)
    {
        _repository.GetDependentSuccessor(
                Arg.Is<DailyClose>(session => session.Id == sessionId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder ListByBranchIdAsNoTrackingReturns(
        Guid branchId,
        DailyCloseListFilter filter,
        IReadOnlyList<DailyClose> result)
    {
        _repository.ListByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(filter, actual)),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder ListByBranchIdAndYearMonthAsNoTrackingReturns(
        Guid branchId,
        int year,
        int month,
        IReadOnlyList<DailyClose> result)
    {
        _repository.ListByBranchIdAndYearMonthAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<int>(value => value == year),
                Arg.Is<int>(value => value == month),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder GetEarliestDateByBranchIdAsNoTrackingReturns(Guid branchId, DateTime? result)
    {
        _repository.GetEarliestDateByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder CountByBranchIdAsNoTrackingReturns(
        Guid branchId,
        DailyCloseListFilter filter,
        int count)
    {
        _repository.CountByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DailyCloseListFilter>(actual => MatchesFilter(filter, actual)),
                Arg.Any<CancellationToken>())
            .Returns(count);
        return this;
    }

    public DailyClosesRepositoryBuilder ListCloseSummariesByBranchIdAndDateRangeAsNoTrackingReturns(
        Guid branchId,
        DateTime dateFrom,
        DateTime dateTo,
        int? take,
        IReadOnlyList<DailyCloseSummaryRow> result)
    {
        _repository.ListCloseSummariesByBranchIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DateTime>(value => value == dateFrom),
                Arg.Is<DateTime>(value => value == dateTo),
                Arg.Is<int?>(value => value == take),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public DailyClosesRepositoryBuilder CountByStatusByBranchIdAndDateRangeAsNoTrackingReturns(
        Guid branchId,
        DateTime dateFrom,
        DateTime dateTo,
        IReadOnlyList<DailyCloseStatusCountRow> result)
    {
        _repository.CountByStatusByBranchIdAndDateRangeAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Is<DateTime>(value => value == dateFrom),
                Arg.Is<DateTime>(value => value == dateTo),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public IDailyClosesRepository Build()
    {
        return _repository;
    }

    private static bool MatchesFilter(DailyCloseListFilter expected, DailyCloseListFilter actual)
    {
        return expected.AccountId == actual.AccountId &&
               expected.Status == actual.Status &&
               expected.DateFrom == actual.DateFrom &&
               expected.DateTo == actual.DateTo &&
               expected.OperatorId == actual.OperatorId &&
               expected.Mine == actual.Mine &&
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
}
