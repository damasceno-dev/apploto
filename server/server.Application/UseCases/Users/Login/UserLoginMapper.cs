using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Users.Login;

public static class UserLoginMapper
{
    public static ResponseUserLoginJson ToResponse(this User user, string token, string refreshToken)
    {
        return new ResponseUserLoginJson
        {
            Name = user.Name,
            Email = user.Email,
            ResponseToken = new ResponseTokenJson
            {
                Token = token,
                RefreshToken = refreshToken
            }
        };
    }
}