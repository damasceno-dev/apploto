using server.Communication.Requests;
using server.Domain.Entities;

namespace server.Application.UseCases.Categories.Create;

public static class CreateCategoryMapper
{
    public static Category ToDomain(this RequestCreateCategoryJson request, Guid branchId)
    {
        return new Category
        {
            Name = request.Name.Trim(),
            DefaultDirection = request.DefaultDirection,
            BranchId = branchId
        };
    }
}
