using Bogus;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Entities;

public class TransactionTypeBuilder
{
    private readonly Faker _faker = new();
    private Guid _id = Guid.NewGuid();
    private string _name;
    private SettlementRule _settlementRule = SettlementRule.SameDay;
    private bool _requiresTabAccountAndClient = false;
    private Guid _categoryId = Guid.NewGuid();
    private Category? _category;
    private bool _active = true;

    public TransactionTypeBuilder()
    {
        _name = _faker.Commerce.ProductName();
    }

    public TransactionTypeBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TransactionTypeBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public TransactionTypeBuilder WithSettlementRule(SettlementRule rule)
    {
        _settlementRule = rule;
        return this;
    }

    public TransactionTypeBuilder WithRequiresTabAccountAndClient(bool value)
    {
        _requiresTabAccountAndClient = value;
        return this;
    }

    public TransactionTypeBuilder WithCategoryId(Guid categoryId)
    {
        _categoryId = categoryId;
        _category = null;
        return this;
    }

    public TransactionTypeBuilder WithCategory(Category category)
    {
        _category = category;
        _categoryId = category.Id;
        return this;
    }

    public TransactionTypeBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public TransactionType Build()
    {
        var branch = new BranchBuilder().Build();
        var category = _category ?? new Category
        {
            Id = _categoryId,
            Name = _faker.Commerce.Categories(1)[0],
            DefaultDirection = Direction.In,
            BranchId = branch.Id,
            Branch = branch
        };

        return new TransactionType
        {
            Id = _id,
            Name = _name,
            SettlementRule = _settlementRule,
            RequiresTabAccountAndClient = _requiresTabAccountAndClient,
            CategoryId = _categoryId,
            Category = category,
            Active = _active
        };
    }
}
