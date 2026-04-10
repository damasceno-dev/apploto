using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Branches.GetCurrentBranchSummary;

public static class GetCurrentBranchSummaryMapper
{
    extension(Branch branch)
    {
        public ResponseGetCurrentBranchSummaryJson ToCurrentBranchResponse(Role role)
        {
            return new ResponseGetCurrentBranchSummaryJson
            {
                Branch = branch.ToBranchSummaryResponse(role)
            };
        }
    }
}
