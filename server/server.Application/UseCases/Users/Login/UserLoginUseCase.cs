using server.Application.Services;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Users.Login;

public class UserLoginUseCase(IUsersRepository usersRepository, ITokenServices tokenServices, IRefreshTokenRepository refreshTokenRepository, IUnitOfWork unitOfWork, PasswordEncryption passwordEncryption)
{
    public async Task<ResponseUserLoginJson> Execute(RequestUserLoginJson request)
    {
        Validate(request);
        var userToVerify = await usersRepository.GetByEmail(request.Email);
        var user = VerifyUserAndPassword(userToVerify, request.Password);

        var token = tokenServices.Generate(user);
        var refreshToken = refreshTokenRepository.Generate();
        await refreshTokenRepository.SaveRefreshToken(new RefreshToken
        {
            Value = refreshToken,
            UserId = user.Id
        });
        await unitOfWork.Commit();
        
        return user.ToResponse(token, refreshToken);
    }

    private User VerifyUserAndPassword(User? user, string requestPassword)
    {
        if (user is null)
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