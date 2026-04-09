using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class TransactionTypesRepository(ServerDbContext dbContext) : ITransactionTypesRepository
{
    public async Task AddRange(IEnumerable<TransactionType> transactionTypes)
    {
        await dbContext.TransactionTypes.AddRangeAsync(transactionTypes);
    }
}
