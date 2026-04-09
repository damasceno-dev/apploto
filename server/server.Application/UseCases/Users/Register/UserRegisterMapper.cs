using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Users.Register;

public static class UserRegisterMapper
{
    public static User ToDomain(this RequestUserRegisterJson request)
    {
        return new User
        {
            Name = request.Name,
            Email = request.Email,
            Password = request.Password
        };
    }

    public static ResponseUserRegisterJson ToResponse(this User user, string token, string refreshToken)
    {
        return new ResponseUserRegisterJson
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
