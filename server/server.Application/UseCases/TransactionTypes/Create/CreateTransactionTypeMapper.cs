using server.Communication.Requests;
using server.Domain.Entities;

namespace server.Application.UseCases.TransactionTypes.Create;

internal static class CreateTransactionTypeMapper
{
    public static TransactionType ToDomain(this RequestCreateTransactionTypeJson request)
    {
        return new TransactionType
        {
            Name = request.Name.Trim(),
            SettlementRule = request.SettlementRule,
            RequiresTabAccountAndClient = request.RequiresTabAccountAndClient,
            CategoryId = request.CategoryId
        };
    }
}
