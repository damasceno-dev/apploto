using Bogus;
using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestCreateTransactionTypeJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private Guid _categoryId = Guid.NewGuid();
    private string _name;
    private SettlementRule _settlementRule = SettlementRule.SameDay;
    private bool _requiresTabAccountAndClient = false;

    public RequestCreateTransactionTypeJsonBuilder()
    {
        _name = _faker.Commerce.ProductName();
    }

    public RequestCreateTransactionTypeJsonBuilder WithCategoryId(Guid categoryId)
    {
        _categoryId = categoryId;
        return this;
    }

    public RequestCreateTransactionTypeJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestCreateTransactionTypeJsonBuilder WithSettlementRule(SettlementRule rule)
    {
        _settlementRule = rule;
        return this;
    }

    public RequestCreateTransactionTypeJsonBuilder WithRequiresTabAccountAndClient(bool value)
    {
        _requiresTabAccountAndClient = value;
        return this;
    }

    public RequestCreateTransactionTypeJson Build()
    {
        return new RequestCreateTransactionTypeJson
        {
            CategoryId = _categoryId,
            Name = _name,
            SettlementRule = _settlementRule,
            RequiresTabAccountAndClient = _requiresTabAccountAndClient
        };
    }
}
