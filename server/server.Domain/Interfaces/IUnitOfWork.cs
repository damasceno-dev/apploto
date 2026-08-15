namespace server.Domain.Interfaces;

public interface IUnitOfWork
{
    Task Commit(CancellationToken ct = default);
}
