using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class OperatorAccountBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _operatorId = Guid.NewGuid();
    private Operator? _operator;
    private Guid _accountId = Guid.NewGuid();
    private Account? _account;
    private bool _isPrimary = false;
    private bool _active = true;

    public OperatorAccountBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public OperatorAccountBuilder WithOperatorId(Guid operatorId)
    {
        _operatorId = operatorId;
        _operator = null;
        return this;
    }

    public OperatorAccountBuilder WithOperator(Operator op)
    {
        _operator = op;
        _operatorId = op.Id;
        return this;
    }

    public OperatorAccountBuilder WithAccountId(Guid accountId)
    {
        _accountId = accountId;
        _account = null;
        return this;
    }

    public OperatorAccountBuilder WithAccount(Account account)
    {
        _account = account;
        _accountId = account.Id;
        return this;
    }

    public OperatorAccountBuilder WithIsPrimary(bool isPrimary)
    {
        _isPrimary = isPrimary;
        return this;
    }

    public OperatorAccountBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public OperatorAccount Build()
    {
        var account = _account ?? new AccountBuilder().WithId(_accountId).Build();
        var op = _operator ?? new OperatorBuilder().WithId(_operatorId).Build();

        return new OperatorAccount
        {
            Id = _id,
            OperatorId = _operatorId,
            Operator = op,
            AccountId = _accountId,
            Account = account,
            IsPrimary = _isPrimary,
            Active = _active
        };
    }
}
