using server.Domain.Entities;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class BankMovementsRepository(ServerDbContext db) : IBankMovementsRepository
{
    public async Task Add(BankMovement movement, CancellationToken ct) => await db.BankMovements.AddAsync(movement, ct);
}
