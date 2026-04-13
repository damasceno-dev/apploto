using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class OperatorAccountsRepositoryBuilder
{
    private readonly IOperatorAccountsRepository _repository = Substitute.For<IOperatorAccountsRepository>();

    public OperatorAccountsRepositoryBuilder GetByOperatorIdAndAccountId(OperatorAccount? link)
    {
        _repository.GetByOperatorIdAndAccountId(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(link);
        return this;
    }

    public OperatorAccountsRepositoryBuilder GetActivePrimaryByOperatorId(OperatorAccount? link)
    {
        _repository.GetActivePrimaryByOperatorId(Arg.Any<Guid>()).Returns(link);
        return this;
    }

    public OperatorAccountsRepositoryBuilder ListActiveByOperatorId(IReadOnlyList<OperatorAccount> links)
    {
        _repository.ListActiveByOperatorId(Arg.Any<Guid>()).Returns(links);
        return this;
    }

    public OperatorAccountsRepositoryBuilder ListActiveByAccountId(IReadOnlyList<OperatorAccount> links)
    {
        _repository.ListActiveByAccountId(Arg.Any<Guid>()).Returns(links);
        return this;
    }

    public OperatorAccountsRepositoryBuilder ListActiveByOperatorIdWithAccount(IReadOnlyList<OperatorAccount> links)
    {
        _repository.ListActiveByOperatorIdWithAccount(Arg.Any<Guid>()).Returns(links);
        return this;
    }

    public IOperatorAccountsRepository Build()
    {
        return _repository;
    }
}
