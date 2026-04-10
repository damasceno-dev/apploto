using NSubstitute;
using server.Domain.Interfaces;

namespace CommonTestUtilities.Repositories;

public class ProductsRepositoryBuilder
{
    private readonly IProductsRepository _repository = Substitute.For<IProductsRepository>();

    public IProductsRepository Build()
    {
        return _repository;
    }
}
