using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Branches.Create;

public static class CreateBranchMapper
{
    public static Branch ToDomain(this RequestCreateBranchJson request)
    {
        return new Branch
        {
            Name = request.Name,
            Cnpj = request.Cnpj,
            Address = request.Address,
            Phone = request.Phone
        };
    }

    extension(Branch branch)
    {
        public BranchUser ToCreatorBranchUser(Guid userId)
        {
            return new BranchUser
            {
                UserId = userId,
                BranchId = branch.Id,
                Role = Role.Admin
            };
        }

        public ResponseCreateBranchJson ToResponse()
        {
            return new ResponseCreateBranchJson
            {
                Id = branch.Id,
                Name = branch.Name,
                Cnpj = branch.Cnpj,
                Address = branch.Address,
                Phone = branch.Phone
            };
        }
    }
}
