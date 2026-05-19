using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.TransactionTypes;

internal static class TransactionTypeSharedMapper
{
    extension(TransactionType tt)
    {
        public ResponseTransactionTypeJson ToResponse()
        {
            return new ResponseTransactionTypeJson
            {
                Id = tt.Id,
                Name = tt.Name,
                SettlementRule = tt.SettlementRule,
                RequiresTabAccountAndClient = tt.RequiresTabAccountAndClient,
                CategoryId = tt.CategoryId,
                CategoryName = tt.Category?.Name ?? string.Empty,
                CreatedAt = tt.CreatedAt,
                Active = tt.Active
            };
        }
    }

    extension(IEnumerable<TransactionType> tts)
    {
        public ResponseListTransactionTypesJson ToListResponse()
        {
            return new ResponseListTransactionTypesJson
            {
                Items = tts.Select(tt => tt.ToResponse()).ToList()
            };
        }
    }
}
