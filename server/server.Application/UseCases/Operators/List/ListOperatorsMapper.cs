using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Operators.List;

public static class ListOperatorsMapper
{
    extension(IEnumerable<Operator> operators)
    {
        public ResponseListOperatorsJson ToResponse()
        {
            return new ResponseListOperatorsJson
            {
                Operators = operators
                    .Select(op => op.ToOperatorResponse())
                    .ToList()
            };
        }
    }
}
