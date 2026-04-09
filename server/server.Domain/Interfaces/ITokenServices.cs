using server.Domain.Entities;
using server.Domain.Models;

namespace server.Domain.Interfaces;

public interface ITokenServices
{
    string GenerateGlobalToken(User user);
    string GenerateBranchToken(BranchUser branchUser);
    TokenResultValidation ValidateToken(string token);
}
