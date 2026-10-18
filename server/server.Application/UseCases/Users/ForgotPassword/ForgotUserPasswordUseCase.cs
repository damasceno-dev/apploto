using server.Application.Services.Security;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Users.ForgotPassword;

public sealed class ForgotUserPasswordUseCase(
    IUsersRepository users,
    IAccountSecurityRepository security,
    ISecurityCoordination coordination,
    IUnitOfWork unitOfWork,
    TimeProvider time,
    AccountSecurityOptions options,
    IRecoveryMailQueue mail)
{
    public async Task Execute(RequestForgotPasswordJson request)
    {
        SecurityValidation.Validate(new ForgotPasswordFluentValidation(), request);
        var email = await users.NormalizeEmail(request.Email);
        string? token = null;
        // Throttle records exist for every target, independent of account existence/active state.
        await using (var scope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(email)))
        {
            var now = time.GetUtcNow().UtcDateTime;
            var key = SecurityRules.Hash(email);
            var throttle = await security.GetThrottle(key);
            if (throttle is not null && throttle.NextAllowedAt > now) return;
            if (throttle is null)
            {
                throttle = new RecoveryThrottle { KeyHash = key, CreatedAt = now };
                await security.AddThrottle(throttle);
            }
            throttle.NextAllowedAt = now.Add(options.RecoveryCooldown);
            var user = await users.GetByEmail(email);
            if (user is { Active: true })
            {
                token = SecurityRules.NewToken();
                await security.InvalidateRecovery(user.Id);
                await security.AddReset(new PasswordResetToken
                {
                    UserId = user.Id, TokenHash = SecurityRules.Hash(token), CreatedAt = now,
                    ExpiresAt = now.Add(options.ResetLifetime)
                });
            }
            await unitOfWork.Commit();
            await scope.Complete();
        }
        // Delivery is off the observable request path, bounded and volatile; 202 never asserts delivery.
        if (token is not null)
            mail.TryEnqueue(email, "Para redefinir sua senha, abra: " + options.Link("reset-password", token));
    }
}
