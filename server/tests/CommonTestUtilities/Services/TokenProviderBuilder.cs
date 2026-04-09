using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Services;

public class TokenProviderBuilder
{
    private readonly ITokenProvider _provider = Substitute.For<ITokenProvider>();

    public TokenProviderBuilder WithTokenValue(string? token)
    {
        _provider.GetTokenValue().Returns(token);
        return this;
    }

    public TokenProviderBuilder WithCachedUser(User? user)
    {
        _provider.GetCachedUser().Returns(user);
        return this;
    }

    public TokenProviderBuilder WithCachedBranchUser(BranchUser? branchUser)
    {
        _provider.GetCachedBranchUser().Returns(branchUser);
        return this;
    }

    public ITokenProvider Build()
    {
        return _provider;
    }
}
