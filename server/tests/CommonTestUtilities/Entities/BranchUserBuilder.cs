using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Entities;

public class BranchUserBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _userId = Guid.NewGuid();
    private Guid _branchId = Guid.NewGuid();
    private User? _user;
    private Branch? _branch;
    private Role _role = Role.Member;
    private bool _active = true;

    public BranchUserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public BranchUserBuilder WithUser(User user)
    {
        _user = user;
        _userId = user.Id;
        return this;
    }

    public BranchUserBuilder WithUserId(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public BranchUserBuilder WithBranch(Branch branch)
    {
        _branch = branch;
        _branchId = branch.Id;
        return this;
    }

    public BranchUserBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        return this;
    }

    public BranchUserBuilder WithRole(Role role)
    {
        _role = role;
        return this;
    }

    public BranchUserBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public BranchUser Build()
    {
        var user = _user ?? new UserBuilder().WithId(_userId).Build();
        var branch = _branch ?? new BranchBuilder().WithId(_branchId).Build();

        return new BranchUser
        {
            Id = _id,
            UserId = _userId,
            User = user,
            BranchId = _branchId,
            Branch = branch,
            Role = _role,
            Active = _active
        };
    }
}
