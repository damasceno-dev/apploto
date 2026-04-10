using Bogus;
using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestAddBranchUserJsonBuilder
{
    private readonly Faker _faker = new();
    private Guid? _userId = Guid.NewGuid();
    private string? _email;
    private Role? _role = Role.Member;

    public RequestAddBranchUserJsonBuilder WithUserId(Guid? userId)
    {
        _userId = userId;
        return this;
    }

    public RequestAddBranchUserJsonBuilder WithEmail(string? email)
    {
        _email = email;
        return this;
    }

    public RequestAddBranchUserJsonBuilder WithRandomEmail()
    {
        _email = _faker.Internet.Email();
        _userId = null;
        return this;
    }

    public RequestAddBranchUserJsonBuilder WithRole(Role? role)
    {
        _role = role;
        return this;
    }

    public RequestAddBranchUserJson Build()
    {
        return new RequestAddBranchUserJson
        {
            UserId = _userId,
            Email = _email,
            Role = _role
        };
    }
}
