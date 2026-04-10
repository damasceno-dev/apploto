using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.BranchUsers.UpdateRole;

public static class UpdateBranchUserRoleMapper
{
    extension(BranchUser branchUser)
    {
        public ResponseUpdateBranchUserRoleJson ToUpdateResponse()
        {
            return new ResponseUpdateBranchUserRoleJson
            {
                BranchUser = branchUser.ToResponse()
            };
        }
    }
}
