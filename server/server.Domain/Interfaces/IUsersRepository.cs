using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IUsersRepository
{
    Task Register(User user);
    Task<bool> VerifyIfEmailAlreadyExists(string email);
    Task<User?> GetByEmail(string email);
    Task<User?> GetById(Guid userId);
}