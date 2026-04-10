using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.BranchUsers;

public static class BranchUserSharedMapper
{
        extension(BranchUser branchUser)
        {
            public ResponseBranchUserJson ToResponse()
            {
                return branchUser.ToResponse(branchUser.User);
            }

            public ResponseBranchUserJson ToResponse(User user)
            {
                return new ResponseBranchUserJson
                {
                    Id = branchUser.Id,
                    UserId = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Role = branchUser.Role
                };
            }
        }
}
