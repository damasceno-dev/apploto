using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Application.Services.Security;

public sealed class AccountPasswordUpdater(IAccountSecurityRepository security, PasswordEncryption passwords)
{
    public async Task Execute(User user, string password)
    {
        user.Password = passwords.HashPassword(password);
        await security.RevokeAllRefreshTokens(user.Id);
        await security.InvalidateRecovery(user.Id);
    }
}
