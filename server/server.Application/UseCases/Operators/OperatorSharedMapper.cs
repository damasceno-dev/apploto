using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Operators;

public static class OperatorSharedMapper
{
    extension(Operator op)
    {
        public ResponseOperatorJson ToOperatorResponse()
        {
            return new ResponseOperatorJson
            {
                Id = op.Id,
                Name = op.Name,
                BranchId = op.BranchId,
                UserId = op.UserId
            };
        }
    }
}
