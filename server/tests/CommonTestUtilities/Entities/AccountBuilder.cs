using Bogus;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Entities;

public class AccountBuilder
{
    private readonly Faker _faker = new();
    private Guid _id = Guid.NewGuid();
    private AccountType _type = AccountType.Terminal;
    private string _name;
    private string? _institution;
    private string? _number;
    private Guid _branchId = Guid.NewGuid();
    private Branch? _branch;
    private Guid? _tabAccountId;
    private bool _active = true;

    public AccountBuilder()
    {
        _name = _faker.Commerce.ProductName();
    }

    public AccountBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public AccountBuilder WithType(AccountType type)
    {
        _type = type;
        return this;
    }

    public AccountBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public AccountBuilder WithInstitution(string? institution)
    {
        _institution = institution;
        return this;
    }

    public AccountBuilder WithNumber(string? number)
    {
        _number = number;
        return this;
    }

    public AccountBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        _branch = null;
        return this;
    }

    public AccountBuilder WithBranch(Branch branch)
    {
        _branch = branch;
        _branchId = branch.Id;
        return this;
    }

    public AccountBuilder WithTabAccountId(Guid? tabAccountId)
    {
        _tabAccountId = tabAccountId;
        return this;
    }

    public AccountBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public Account Build()
    {
        var branch = _branch ?? new BranchBuilder().WithId(_branchId).Build();

        return new Account
        {
            Id = _id,
            Type = _type,
            Name = _name,
            Institution = _institution,
            Number = _number,
            BranchId = _branchId,
            Branch = branch,
            TabAccountId = _tabAccountId,
            Active = _active
        };
    }
}
