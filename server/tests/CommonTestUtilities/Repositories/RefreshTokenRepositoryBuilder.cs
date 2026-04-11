using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class RefreshTokenRepositoryBuilder
{
    private readonly IRefreshTokenRepository _repository = Substitute.For<IRefreshTokenRepository>();

    public RefreshTokenRepositoryBuilder Generate(string value)
    {
        _repository.Generate().Returns(value);
        return this;
    }

    public RefreshTokenRepositoryBuilder GetRefreshTokenEntity(RefreshToken? refreshToken)
    {
        _repository.GetRefreshTokenEntity(Arg.Any<string>()).Returns(refreshToken);
        return this;
    }

    public IRefreshTokenRepository Build()
    {
        return _repository;
    }
}
