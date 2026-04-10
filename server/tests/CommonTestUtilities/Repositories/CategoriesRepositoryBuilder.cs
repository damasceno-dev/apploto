using NSubstitute;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class CategoriesRepositoryBuilder
{
    private readonly ICategoriesRepository _repository = Substitute.For<ICategoriesRepository>();

    public ICategoriesRepository Build()
    {
        return _repository;
    }
}
