using Bogus;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Entities;

public class CategoryBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private Guid _id = Guid.NewGuid();
    private string _name;
    private Direction _defaultDirection = Direction.In;
    private Guid _branchId = Guid.NewGuid();
    private bool _active = true;
    private ICollection<TransactionType> _transactionTypes = [];

    public CategoryBuilder()
    {
        _name = _faker.Commerce.Department();
    }

    public CategoryBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public CategoryBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CategoryBuilder WithDefaultDirection(Direction direction)
    {
        _defaultDirection = direction;
        return this;
    }

    public CategoryBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        return this;
    }

    public CategoryBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public CategoryBuilder WithTransactionTypes(ICollection<TransactionType> transactionTypes)
    {
        _transactionTypes = transactionTypes;
        return this;
    }

    public Category Build()
    {
        return new Category
        {
            Id = _id,
            Name = _name,
            DefaultDirection = _defaultDirection,
            BranchId = _branchId,
            Active = _active,
            TransactionTypes = _transactionTypes
        };
    }
}
