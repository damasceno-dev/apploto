using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class OperatorAccountsRepositoryBuilder
{
    private readonly IOperatorAccountsRepository _repository = Substitute.For<IOperatorAccountsRepository>();

    public OperatorAccountsRepositoryBuilder GetByOperatorIdAndAccountId(Guid operatorId, Guid accountId, OperatorAccount? link)
    {
        _repository.GetByOperatorIdAndAccountId(operatorId, accountId).Returns(link);
        return this;
    }

    public OperatorAccountsRepositoryBuilder GetActivePrimaryByOperatorId(Guid operatorId, OperatorAccount? link)
    {
        _repository.GetActivePrimaryByOperatorId(operatorId).Returns(link);
        return this;
    }

    public OperatorAccountsRepositoryBuilder ListActiveByOperatorId(Guid operatorId, IReadOnlyList<OperatorAccount> links)
    {
        _repository.ListActiveByOperatorId(operatorId).Returns(links);
        return this;
    }

    public OperatorAccountsRepositoryBuilder ListActiveByOperatorIdAsNoTracking(Guid operatorId, IReadOnlyList<OperatorAccount> links)
    {
        _repository.ListActiveByOperatorIdAsNoTracking(operatorId).Returns(links);
        return this;
    }

    public OperatorAccountsRepositoryBuilder ListActiveByAccountId(Guid accountId, IReadOnlyList<OperatorAccount> links)
    {
        _repository.ListActiveByAccountId(accountId).Returns(links);
        return this;
    }

    public OperatorAccountsRepositoryBuilder ListActiveByOperatorIdWithAccount(Guid operatorId, IReadOnlyList<OperatorAccount> links)
    {
        _repository.ListActiveByOperatorIdWithAccount(operatorId).Returns(links);
        return this;
    }

    public IOperatorAccountsRepository Build()
    {
        return _repository;
    }
}
