using NSubstitute;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class UsersRepositoryBuilder
{
    private readonly IUsersRepository _repository = Substitute.For<IUsersRepository>();

    public UsersRepositoryBuilder GetById(User? user)
    {
        _repository.GetById(Arg.Any<Guid>()).Returns(user);
        return this;
    }

    public UsersRepositoryBuilder GetById(Guid userId, User? user)
    {
        _repository.GetById(userId).Returns(user);
        return this;
    }

    public UsersRepositoryBuilder GetByEmail(User? user)
    {
        _repository.GetByEmail(Arg.Any<string>()).Returns(user);
        return this;
    }

    public UsersRepositoryBuilder GetByEmail(string email, User? user)
    {
        _repository.GetByEmail(email).Returns(user);
        return this;
    }

    public UsersRepositoryBuilder VerifyIfEmailAlreadyExists(bool alreadyExists)
    {
        _repository.VerifyIfEmailAlreadyExists(Arg.Any<string>()).Returns(alreadyExists);
        return this;
    }

    public IUsersRepository Build()
    {
        return _repository;
    }
}
