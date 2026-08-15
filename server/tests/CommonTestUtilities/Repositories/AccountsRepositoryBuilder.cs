using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models.Projections;

namespace CommonTestUtilities.Repositories;

public class AccountsRepositoryBuilder
{
    private readonly IAccountsRepository _repository = Substitute.For<IAccountsRepository>();

    public AccountsRepositoryBuilder()
    {
        _repository.ListActiveTerminalIdsByTabAccountIds(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(new Dictionary<Guid, Guid>());
        _repository.ListExpectedClosersByBranchIdAsNoTracking(
                Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new List<ExpectedCloserRow>());
    }

    public AccountsRepositoryBuilder GetActiveByIdAndBranchId(Guid id, Guid branchId, Account? account)
    {
        _repository.GetActiveByIdAndBranchId(id, branchId, Arg.Any<CancellationToken>()).Returns(account);
        return this;
    }

    public AccountsRepositoryBuilder GetActiveByIdAndBranchIdAsNoTracking(Account? account)
    {
        _repository.GetActiveByIdAndBranchIdAsNoTracking(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(account);
        _repository.GetActiveByIdAndBranchId(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(account);
        return this;
    }

    public AccountsRepositoryBuilder GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, Account? account)
    {
        _repository.GetActiveByIdAndBranchIdAsNoTracking(id, branchId, Arg.Any<CancellationToken>()).Returns(account);
        _repository.GetActiveByIdAndBranchId(id, branchId, Arg.Any<CancellationToken>()).Returns(account);
        return this;
    }

    public AccountsRepositoryBuilder GetActiveTerminalIdByTabAccountId(Guid tabAccountId, Guid branchId, Guid? terminalAccountId)
    {
        _repository.GetActiveTerminalIdByTabAccountId(tabAccountId, branchId).Returns(terminalAccountId);
        return this;
    }

    public AccountsRepositoryBuilder ListActiveByBranchId(Guid branchId, IReadOnlyList<Account> accounts)
    {
        _repository.ListActiveByBranchId(branchId).Returns(accounts);
        return this;
    }

    public AccountsRepositoryBuilder ListActiveTerminalIdsByTabAccountIds(IReadOnlyDictionary<Guid, Guid> terminalIdsByTabAccountId)
    {
        _repository.ListActiveTerminalIdsByTabAccountIds(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>())
            .Returns(terminalIdsByTabAccountId);
        return this;
    }

    public AccountsRepositoryBuilder ListActiveTerminalIdsByTabAccountIds(
        Guid branchId,
        IReadOnlyCollection<Guid> tabAccountIds,
        IReadOnlyDictionary<Guid, Guid> terminalIdsByTabAccountId)
    {
        _repository.ListActiveTerminalIdsByTabAccountIds(
                branchId,
                Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.OrderBy(i => i).SequenceEqual(tabAccountIds.OrderBy(i => i))))
            .Returns(terminalIdsByTabAccountId);
        return this;
    }

    public AccountsRepositoryBuilder ListExpectedClosersByBranchIdAsNoTrackingReturns(
        Guid branchId,
        IReadOnlyList<ExpectedCloserRow> result)
    {
        _repository.ListExpectedClosersByBranchIdAsNoTracking(
                Arg.Is<Guid>(value => value == branchId),
                Arg.Any<CancellationToken>())
            .Returns(result);
        return this;
    }

    public IAccountsRepository Build()
    {
        return _repository;
    }
}
