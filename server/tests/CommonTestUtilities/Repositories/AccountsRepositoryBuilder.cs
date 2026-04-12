using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class AccountsRepositoryBuilder
{
    private readonly IAccountsRepository _repository = Substitute.For<IAccountsRepository>();

    public AccountsRepositoryBuilder GetActiveByIdAndBranchId(Account? account)
    {
        _repository.GetActiveByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(account);
        return this;
    }

    public AccountsRepositoryBuilder GetActiveByIdAndBranchIdAsNoTracking(Account? account)
    {
        _repository.GetActiveByIdAndBranchIdAsNoTracking(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(account);
        return this;
    }

    public AccountsRepositoryBuilder ListActiveByBranchId(IReadOnlyList<Account> accounts)
    {
        _repository.ListActiveByBranchId(Arg.Any<Guid>()).Returns(accounts);
        return this;
    }

    public AccountsRepositoryBuilder ExistsActiveTerminalForTabAccount(bool exists)
    {
        _repository.ExistsActiveTerminalForTabAccount(Arg.Any<Guid>(), Arg.Any<Guid?>()).Returns(exists);
        return this;
    }

    public IAccountsRepository Build()
    {
        return _repository;
    }
}
