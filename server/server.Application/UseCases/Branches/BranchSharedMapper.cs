using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Branches;

public static class BranchSharedMapper
{
    extension(BranchUser branchUser)
    {
        public ResponseBranchSummaryJson ToBranchSummaryResponse()
        {
            return new ResponseBranchSummaryJson
            {
                Id = branchUser.Branch.Id,
                Name = branchUser.Branch.Name,
                Cnpj = branchUser.Branch.Cnpj,
                Address = branchUser.Branch.Address,
                Phone = branchUser.Branch.Phone,
                Role = branchUser.Role
            };
        }
    }

    extension(Branch branch)
    {
        public ResponseBranchSummaryJson ToBranchSummaryResponse(Role role)
        {
            return new ResponseBranchSummaryJson
            {
                Id = branch.Id,
                Name = branch.Name,
                Cnpj = branch.Cnpj,
                Address = branch.Address,
                Phone = branch.Phone,
                Role = role
            };
        }
    }
}
