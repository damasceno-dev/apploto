using server.Application.Services.Security;
using server.Application.Services;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Users.Register;

public class UserRegisterUseCase(
    IUsersRepository usersRepository,
    ITokenServices tokenServices,
    IRefreshTokenRepository refreshTokenRepository,
    IUnitOfWork unitOfWork,
    PasswordEncryption passwordEncryption,
    ISecurityCoordination coordination)
{
    public async Task<ResponseUserRegisterJson> Execute(RequestUserRegisterJson request)
    {
        Validate(request);
        var email = await usersRepository.NormalizeEmail(request.Email);
        await using var scope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(email));
        if (await usersRepository.VerifyIfEmailAlreadyExists(email))
            throw new ConflictException(ResourcesErrorMessages.EMAIL_ALREADY_REGISTERED);

        var newUser = request.ToDomain(email);
        newUser.Password = passwordEncryption.HashPassword(request.Password);

        var token = tokenServices.GenerateGlobalToken(newUser);
        var refreshToken = refreshTokenRepository.Generate();

        await usersRepository.Register(newUser);
        await refreshTokenRepository.SaveRefreshToken(new RefreshToken
        {
            Value = refreshToken,
            UserId = newUser.Id
        });

        await unitOfWork.Commit();
        await scope.Complete();
        return newUser.ToResponse(token, refreshToken);
    }

    private static void Validate(RequestUserRegisterJson request)
    {
        var result = new UserRegisterFluentValidation().Validate(request);
        if (result.IsValid is false)
        {
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
        }
    }
}
