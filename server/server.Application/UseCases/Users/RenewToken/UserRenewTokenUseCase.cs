using server.Application.Services;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Users.RenewToken;

public class UserRenewTokenUseCase(IRefreshTokenRepository refreshTokenRepository, ITokenServices tokenServices ,IUsersRepository usersRepository, IUnitOfWork unitOfWork)
{
    public async Task<ResponseTokenJson> Execute(RequestUserRenewTokenJson request)
    {
        var refreshTokenEntity = await refreshTokenRepository.GetRefreshTokenEntity(request.Value)
                                 ?? throw new RefreshTokenException(ResourcesErrorMessages.REFRESHTOKEN_INVALID);
        
        if (refreshTokenEntity.CreatedAt.AddDays(SharedValidators.RefreshTokenExpirationTimeInDays) < DateTime.UtcNow)
        {
            throw new RefreshTokenException(ResourcesErrorMessages.REFRESHTOKEN_EXPIRED);
        }
        
        var user = await usersRepository.GetById(refreshTokenEntity.UserId) ??
                   throw new RefreshTokenException(ResourcesErrorMessages.REFRESHTOKEN_WITHOUT_USER);

        
        var token = tokenServices.GenerateGlobalToken(user);
        var refreshToken = refreshTokenRepository.Generate();
        await refreshTokenRepository.SaveRefreshToken(new RefreshToken
        {
            Value = refreshToken,
            UserId = user.Id
        });
        await unitOfWork.Commit();
        
        return new ResponseTokenJson
        {
            Token = token,
            RefreshToken = refreshToken
        };
    }
}
