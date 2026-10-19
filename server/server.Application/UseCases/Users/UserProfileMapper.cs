using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Users;

public static class UserProfileMapper
{
    extension(User user)
    {
        public ResponseUserProfileJson ToProfileResponse() => new() { UserId = user.Id, Name = user.Name, Email = user.Email, IsPlatformOperator = user.IsPlatformOperator, HasPassword = !string.IsNullOrEmpty(user.Password) };
    }
}
