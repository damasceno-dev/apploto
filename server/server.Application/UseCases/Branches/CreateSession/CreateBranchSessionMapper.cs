using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Branches.CreateSession;

public static class CreateBranchSessionMapper
{
    extension(Branch branch)
    {
        public ResponseCreateBranchSessionJson ToSessionResponse(Role role, string token)
        {
            return new ResponseCreateBranchSessionJson
            {
                Token = token,
                Branch = branch.ToBranchSummaryResponse(role)
            };
        }
    }
}
