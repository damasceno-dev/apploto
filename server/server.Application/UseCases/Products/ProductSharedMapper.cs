using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Products;

internal static class ProductSharedMapper
{
    extension(Product product)
    {
        public ResponseProductJson ToResponse()
        {
            return new ResponseProductJson
            {
                Id = product.Id,
                Name = product.Name,
                DisplayOrder = product.DisplayOrder,
                BranchId = product.BranchId,
                CreatedAt = product.CreatedAt,
                Active = product.Active
            };
        }
    }

    extension(IEnumerable<Product> products)
    {
        public ResponseListProductsJson ToListResponse()
        {
            return new ResponseListProductsJson
            {
                Items = products.Select(p => p.ToResponse()).ToList()
            };
        }
    }
}
