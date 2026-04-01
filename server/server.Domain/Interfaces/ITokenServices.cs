using server.Domain.Entities;
using server.Domain.Models;

namespace server.Domain.Interfaces;

public interface ITokenServices
{
    string Generate(User user);
    TokenResultValidation ValidateToken(string token);
}