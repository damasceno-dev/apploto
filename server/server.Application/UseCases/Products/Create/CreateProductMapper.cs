using server.Communication.Requests;
using server.Domain.Entities;

namespace server.Application.UseCases.Products.Create;

internal static class CreateProductMapper
{
    public static Product ToDomain(this RequestCreateProductJson request, Guid branchId)
    {
        return new Product
        {
            Name = request.Name.Trim(),
            DisplayOrder = request.DisplayOrder,
            BranchId = branchId
        };
    }
}
