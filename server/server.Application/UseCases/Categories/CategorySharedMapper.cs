using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Categories;

internal static class CategorySharedMapper
{
    extension(Category category)
    {
        public ResponseCategoryJson ToResponse()
        {
            return new ResponseCategoryJson
            {
                Id = category.Id,
                Name = category.Name,
                DefaultDirection = category.DefaultDirection,
                BranchId = category.BranchId,
                CreatedAt = category.CreatedAt,
                Active = category.Active
            };
        }
    }

    extension(IEnumerable<Category> categories)
    {
        public ResponseListCategoriesJson ToListResponse()
        {
            return new ResponseListCategoriesJson
            {
                Items = categories.Select(c => c.ToResponse()).ToList()
            };
        }
    }
}
