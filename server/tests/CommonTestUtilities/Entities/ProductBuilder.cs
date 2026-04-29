using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class ProductBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = $"Product {Guid.NewGuid():N}";
    private int _displayOrder = 1;
    private Guid _branchId = Guid.NewGuid();

    public ProductBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ProductBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ProductBuilder WithDisplayOrder(int displayOrder)
    {
        _displayOrder = displayOrder;
        return this;
    }

    public ProductBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        return this;
    }

    public Product Build()
    {
        return new Product
        {
            Id = _id,
            Name = _name,
            DisplayOrder = _displayOrder,
            BranchId = _branchId
        };
    }
}
