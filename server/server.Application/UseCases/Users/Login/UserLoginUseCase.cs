using server.Application.Services.Security;
using server.Application.Services;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Users.Login;

public class UserLoginUseCase(
    IUsersRepository usersRepository,
    ITokenServices tokenServices,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    PasswordEncryption passwordEncryption,
    ISecurityCoordination coordination,
    LoginAttemptGuard attempts)
{
    private static readonly string UnavailableAccountHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

    public async Task<ResponseUserLoginJson> Execute(RequestUserLoginJson request)
    {
        Validate(request);
        var email = await usersRepository.NormalizeEmail(request.Email);
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(email));
        // The abuse budgets are checked before the account is read or BCrypt runs, the same way for every account state.
        var attempt = await attempts.Admit(email);
        var user = VerifyUserAndPassword(await usersRepository.GetByEmail(email), request.Password);
        if (user is null)
        {
            await attempts.Failed(attempt);
            await unitOfWork.Commit();
            await scope.Complete();
            throw new InvalidLoginException(ResourcesErrorMessages.PASSWORD_WRONG);
        }

        attempts.Succeeded(attempt);
        var token = tokenServices.GenerateGlobalToken(user);
        var refreshToken = refreshTokenRepository.Generate();
        await refreshTokenRepository.SaveRefreshToken(new RefreshToken
        {
            Value = refreshToken,
            UserId = user.Id
        });
        await unitOfWork.Commit();
        await scope.Complete();

        return user.ToResponse(token, refreshToken);
    }

    private User? VerifyUserAndPassword(User? user, string requestPassword)
    {
        // Verify one BCrypt hash even for unavailable accounts so response time does not
        // trivially distinguish them from a wrong password on an active account.
        var usable = user is { Active: true } && !string.IsNullOrEmpty(user.Password);
        var passwordMatches = passwordEncryption.VerifyPassword(requestPassword,
            usable ? user!.Password : UnavailableAccountHash);
        return usable && passwordMatches ? user : null;
    }

    private static void Validate(RequestUserLoginJson request)
    {
        var result = new UserLoginFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
