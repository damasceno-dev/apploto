using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.BranchUsers.Remove;

public static class RemoveBranchUserMapper
{
    extension(BranchUser branchUser)
    {
        public ResponseRemoveBranchUserJson ToRemoveResponse()
        {
            return new ResponseRemoveBranchUserJson
            {
                BranchUser = branchUser.ToResponse()
            };
        }
    }
}
