using Bogus;
using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestUpdateTransactionTypeJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private string _name;
    private SettlementRule _settlementRule = SettlementRule.SameDay;
    private bool _requiresTabAccountAndClient = false;

    public RequestUpdateTransactionTypeJsonBuilder()
    {
        _name = _faker.Commerce.ProductName();
    }

    public RequestUpdateTransactionTypeJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestUpdateTransactionTypeJsonBuilder WithSettlementRule(SettlementRule rule)
    {
        _settlementRule = rule;
        return this;
    }

    public RequestUpdateTransactionTypeJsonBuilder WithRequiresTabAccountAndClient(bool value)
    {
        _requiresTabAccountAndClient = value;
        return this;
    }

    public RequestUpdateTransactionTypeJson Build()
    {
        return new RequestUpdateTransactionTypeJson
        {
            Name = _name,
            SettlementRule = _settlementRule,
            RequiresTabAccountAndClient = _requiresTabAccountAndClient
        };
    }
}
