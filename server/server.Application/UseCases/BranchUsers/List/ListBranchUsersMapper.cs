using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.BranchUsers.List;

public static class ListBranchUsersMapper
{
    extension(IEnumerable<BranchUser> branchUsers)
    {
        public ResponseListBranchUsersJson ToResponse()
        {
            return new ResponseListBranchUsersJson
            {
                BranchUsers = branchUsers
                    .Select(branchUser => branchUser.ToResponse())
                    .ToList()
            };
        }
    }
}
