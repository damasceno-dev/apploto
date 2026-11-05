using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IBankMovementsRepository
{
    Task Add(BankMovement movement, CancellationToken ct);
}
