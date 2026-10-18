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
    ISecurityCoordination coordination)
{
    public async Task<ResponseUserLoginJson> Execute(RequestUserLoginJson request)
    {
        Validate(request);
        var email = await usersRepository.NormalizeEmail(request.Email);
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(email));
        var userToVerify = await usersRepository.GetByEmail(email);
        var user = VerifyUserAndPassword(userToVerify, request.Password);

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

    private User VerifyUserAndPassword(User? user, string requestPassword)
    {
        if (user is null || !user.Active)
        {
            throw new InvalidLoginException(ResourcesErrorMessages.EMAIL_NOT_REGISTERED);
        }

        if (passwordEncryption.VerifyPassword(requestPassword, user.Password) is false)
        {
            throw new InvalidLoginException(ResourcesErrorMessages.PASSWORD_WRONG);
        }

        return user;
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
