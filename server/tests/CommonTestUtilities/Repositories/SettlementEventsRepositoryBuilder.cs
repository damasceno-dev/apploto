using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

/// <summary>Settlement events substitute: no events and no command history unless a test configures them.</summary>
public class SettlementEventsRepositoryBuilder
{
    private readonly ISettlementEventsRepository _repository = Substitute.For<ISettlementEventsRepository>();

    public SettlementEventsRepositoryBuilder()
    {
        _repository.ListByReceivableIdsAndBranchIdAsNoTracking(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SettlementEvent>());
        _repository.ListByBankReceiptIdAndBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SettlementEvent>());
        _repository.ListReturnsOfDepositEventsAsNoTracking(
                Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SettlementEvent>());
    }

    /// <summary>The events of one receivable, returned only for exactly that receivable and branch.</summary>
    public SettlementEventsRepositoryBuilder ListByReceivableReturns(Guid receivableId, Guid branchId, params SettlementEvent[] events)
    {
        _repository.ListByReceivableIdsAndBranchIdAsNoTracking(
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(receivableId)),
                branchId,
                Arg.Any<CancellationToken>())
            .Returns(events);
        return this;
    }

    public SettlementEventsRepositoryBuilder ExistsCommandEventReturns(Guid receivableId, bool exists)
    {
        _repository.ExistsCommandEventByReceivableIdAsNoTracking(receivableId, Arg.Any<CancellationToken>()).Returns(exists);
        return this;
    }

    public ISettlementEventsRepository Build() => _repository;
}
