using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface IProductsRepository
{
    Task AddRange(IEnumerable<Product> products);
}
