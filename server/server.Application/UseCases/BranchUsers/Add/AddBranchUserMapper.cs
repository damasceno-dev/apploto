using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.BranchUsers.Add;

public static class AddBranchUserMapper
{
    public static BranchUser ToDomain(this RequestAddBranchUserJson request, Guid userId, Guid branchId)
    {
        return new BranchUser
        {
            UserId = userId,
            BranchId = branchId,
            Role = request.Role!.Value
        };
    }

    extension(BranchUser branchUser)
    {
        public ResponseAddBranchUserJson ToAddResponse(User user)
        {
            return new ResponseAddBranchUserJson
            {
                BranchUser = branchUser.ToResponse(user)
            };
        }
    }
}
