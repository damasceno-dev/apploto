using Bogus;
using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class OperatorBuilder
{
    private readonly Faker _faker = new();
    private Guid _id = Guid.NewGuid();
    private string _name;
    private Guid _branchId = Guid.NewGuid();
    private Branch? _branch;
    private Guid? _userId;
    private User? _user;
    private bool _active = true;

    public OperatorBuilder()
    {
        _name = _faker.Name.FullName();
    }

    public OperatorBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public OperatorBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public OperatorBuilder WithBranch(Branch branch)
    {
        _branch = branch;
        _branchId = branch.Id;
        return this;
    }

    public OperatorBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        return this;
    }

    public OperatorBuilder WithUser(User user)
    {
        _user = user;
        _userId = user.Id;
        return this;
    }

    public OperatorBuilder WithUserId(Guid? userId)
    {
        _userId = userId;
        _user = null;
        return this;
    }

    public OperatorBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public Operator Build()
    {
        var branch = _branch ?? new BranchBuilder().WithId(_branchId).Build();

        return new Operator
        {
            Id = _id,
            Name = _name,
            BranchId = _branchId,
            Branch = branch,
            UserId = _userId,
            User = _user,
            Active = _active
        };
    }
}
