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
    private SettlementInstrument? _settlementInstrument;
    private MonthlyFinancialRole? _monthlyFinancialRole;

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

    public RequestUpdateTransactionTypeJsonBuilder WithSettlementInstrument(SettlementInstrument? value)
    {
        _settlementInstrument = value;
        return this;
    }

    public RequestUpdateTransactionTypeJsonBuilder WithMonthlyFinancialRole(MonthlyFinancialRole? value)
    {
        _monthlyFinancialRole = value;
        return this;
    }

    public RequestUpdateTransactionTypeJson Build()
    {
        return new RequestUpdateTransactionTypeJson
        {
            Name = _name,
            SettlementRule = _settlementRule,
            RequiresTabAccountAndClient = _requiresTabAccountAndClient,
            SettlementInstrument = _settlementInstrument,
            MonthlyFinancialRole = _monthlyFinancialRole
        };
    }
}
