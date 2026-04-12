using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Operators.Create;

public static class CreateOperatorMapper
{
    public static Operator ToDomain(this RequestCreateOperatorJson request, Guid branchId)
    {
        return new Operator
        {
            Name = request.Name.Trim(),
            BranchId = branchId,
            UserId = request.UserId
        };
    }

    extension(Operator op)
    {
        public ResponseCreateOperatorJson ToResponse()
        {
            return new ResponseCreateOperatorJson
            {
                Id = op.Id,
                Name = op.Name,
                BranchId = op.BranchId,
                UserId = op.UserId
            };
        }
    }
}
