using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ICategoriesRepository
{
    Task AddRange(IEnumerable<Category> categories);
}
