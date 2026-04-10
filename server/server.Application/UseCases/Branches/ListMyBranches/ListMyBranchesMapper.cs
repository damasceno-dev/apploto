using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Branches.ListMyBranches;

public static class ListMyBranchesMapper
{
    extension(IEnumerable<BranchUser> branchUsers)
    {
        public ResponseListMyBranchesJson ToResponse()
        {
            return new ResponseListMyBranchesJson
            {
                Branches = branchUsers
                    .Select(branchUser => branchUser.ToBranchSummaryResponse())
                    .ToList()
            };
        }
    }
}
