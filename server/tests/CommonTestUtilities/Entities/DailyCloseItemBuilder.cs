using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class DailyCloseItemBuilder
{
    private Guid _id = Guid.NewGuid();
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _active = true;
    private decimal _value = 100m;
    private Guid _dailyCloseId = Guid.NewGuid();
    private DailyClose? _dailyClose;
    private Guid _productId = Guid.NewGuid();
    private Product? _product;

    public DailyCloseItemBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public DailyCloseItemBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public DailyCloseItemBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public DailyCloseItemBuilder WithValue(decimal value)
    {
        _value = value;
        return this;
    }

    public DailyCloseItemBuilder WithDailyClose(DailyClose dailyClose)
    {
        _dailyClose = dailyClose;
        _dailyCloseId = dailyClose.Id;
        return this;
    }

    public DailyCloseItemBuilder WithDailyCloseId(Guid dailyCloseId)
    {
        _dailyCloseId = dailyCloseId;
        _dailyClose = null;
        return this;
    }

    public DailyCloseItemBuilder WithProduct(Product product)
    {
        _product = product;
        _productId = product.Id;
        return this;
    }

    public DailyCloseItemBuilder WithProductId(Guid productId)
    {
        _productId = productId;
        _product = null;
        return this;
    }

    public DailyCloseItem Build()
    {
        var product = _product ?? new Product
        {
            Id = _productId,
            Name = $"Product {_productId:N}",
            DisplayOrder = 1,
            BranchId = Guid.NewGuid()
        };

        return new DailyCloseItem
        {
            Id = _id,
            CreatedAt = _createdAt,
            Active = _active,
            Value = _value,
            DailyCloseId = _dailyCloseId,
            DailyClose = _dailyClose!,
            ProductId = _productId,
            Product = product
        };
    }
}
