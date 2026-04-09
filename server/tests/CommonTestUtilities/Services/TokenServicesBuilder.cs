using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Domain.Models;

namespace CommonTestUtilities.Services;

public class TokenServicesBuilder
{
    private readonly ITokenServices _services = Substitute.For<ITokenServices>();

    public TokenServicesBuilder ValidateToken(string token, TokenResultValidation validationResult)
    {
        _services.ValidateToken(token).Returns(validationResult);
        return this;
    }

    public TokenServicesBuilder GenerateGlobalToken(User user, string token)
    {
        _services.GenerateGlobalToken(user).Returns(token);
        return this;
    }

    public TokenServicesBuilder GenerateBranchToken(BranchUser branchUser, string token)
    {
        _services.GenerateBranchToken(branchUser).Returns(token);
        return this;
    }

    public ITokenServices Build()
    {
        return _services;
    }
}
