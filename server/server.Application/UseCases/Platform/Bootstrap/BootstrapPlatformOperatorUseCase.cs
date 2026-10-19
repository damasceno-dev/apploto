using FluentValidation;
using server.Application.Services.Security;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Platform.Bootstrap;

public sealed class BootstrapPlatformOperatorUseCase(IUsersRepository users, IAccountSecurityRepository security,
    ISecurityCoordination coordination, IUnitOfWork unitOfWork, ISecurityMailSender mail,
    AccountSecurityOptions options, TimeProvider time)
{
    public async Task<Guid> Execute(string name, string targetEmail, bool write)
    {
        var validator = new InlineValidator<(string Name, string Email)>();
        validator.RuleFor(x => x.Name).NotEmpty().WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID)
            .MaximumLength(255).WithMessage(ResourcesErrorMessages.ONBOARDING_INPUT_INVALID);
        validator.RuleFor(x => x.Email).NotEmpty().WithMessage(ResourcesErrorMessages.EMAIL_INVALID)
            .EmailAddress().WithMessage(ResourcesErrorMessages.EMAIL_INVALID)
            .MaximumLength(255).WithMessage(ResourcesErrorMessages.EMAIL_INVALID);
        var validation = await validator.ValidateAsync((name, targetEmail));
        if (!validation.IsValid) throw new OnValidationException(validation.Errors.Select(x => x.ErrorMessage).ToList());

        var email = await users.NormalizeEmail(targetEmail);
        string? token = null;
        Guid id;
        await using (var scope = await SecurityRules.Acquire(coordination, "platform-first-operator", SecurityRules.EmailKey(email)))
        {
            var count = await users.CountAll();
            var existing = await users.GetByEmail(email);
            if (count != 0 && !(count == 1 && existing is { Active: true, IsPlatformOperator: true,
                    EmailVerifiedAt: null, Password: "" } && existing.Name == name.Trim()))
                throw new ConflictException(ResourcesErrorMessages.BOOTSTRAP_FIRST_OPERATOR_ONLY);
            id = existing?.Id ?? Guid.NewGuid();
            if (write)
            {
                var now = time.GetUtcNow().UtcDateTime;
                if (existing is null)
                    await users.Register(new User { Id = id, Name = name.Trim(), Email = email,
                        Password = string.Empty, IsPlatformOperator = true, CreatedAt = now });
                else await security.InvalidateRecovery(id);
                token = SecurityRules.NewToken();
                await security.AddReset(new PasswordResetToken
                {
                    UserId = id, TokenHash = SecurityRules.Hash(token), CreatedAt = now,
                    ExpiresAt = now.Add(options.ResetLifetime)
                });
                await unitOfWork.Commit();
            }
            await scope.Complete();
        }
        if (token is not null && !await mail.Send(email, "Ative sua conta de operação", 
            "Para definir sua senha, abra: " + options.Link("reset-password", token)))
            throw new ConflictException(ResourcesErrorMessages.BOOTSTRAP_DELIVERY_FAILED);
        return id;
    }
}
