using server.Application.Services;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Users.Register;

public class UserRegisterUseCase(IUsersRepository usersRepository, ITokenServices tokenServices, IRefreshTokenRepository refreshTokenRepository, IUnitOfWork unitOfWork, PasswordEncryption passwordEncryption)
{
    public async Task<ResponseUserRegisterJson> Execute(RequestUserRegisterJson request)
    {
        await Validate(request);
        var newUser = request.ToDomain();
        newUser.Password = passwordEncryption.HashPassword(request.Password);

        var token = tokenServices.Generate(newUser);
        var refreshToken = refreshTokenRepository.Generate();

        await refreshTokenRepository.SaveRefreshToken(new RefreshToken
        {
            Value = refreshToken,
            UserId = newUser.Id
        });
        await unitOfWork.Commit();
        
        await usersRepository.Register(newUser);
        await unitOfWork.Commit();
        return newUser.ToResponse(token, refreshToken);
    }

    private async Task Validate(RequestUserRegisterJson request)
    {
        var result = await new UserRegisterFluentValidation().ValidateAsync(request);
        if (await usersRepository.VerifyIfEmailAlreadyExists(request.Email))
        {
            throw new ConflictException(ResourcesErrorMessages.EMAIL_ALREADY_REGISTERED);
        }

        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}