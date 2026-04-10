using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

public class AuthenticationServiceBuilder
{
    private readonly IAuthenticationService _service = Substitute.For<IAuthenticationService>();

    public AuthenticationServiceBuilder GetAuthenticatedUser(User user)
    {
        _service.GetAuthenticatedUser().Returns(user);
        return this;
    }

    public AuthenticationServiceBuilder GetAuthenticatedBranchUser(BranchUser branchUser)
    {
        _service.GetAuthenticatedBranchUser().Returns(branchUser);
        return this;
    }

    public IAuthenticationService Build()
    {
        return _service;
    }
}
