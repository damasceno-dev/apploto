using Bogus;
using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestCreateAccountJsonBuilder
{
    private readonly Faker _faker = new();
    private AccountType _type = AccountType.Terminal;
    private string _name;
    private string? _institution;
    private string? _number;
    private Guid? _tabAccountId;

    public RequestCreateAccountJsonBuilder()
    {
        _name = _faker.Commerce.ProductName();
    }

    public RequestCreateAccountJsonBuilder WithType(AccountType type)
    {
        _type = type;
        return this;
    }

    public RequestCreateAccountJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestCreateAccountJsonBuilder WithInstitution(string? institution)
    {
        _institution = institution;
        return this;
    }

    public RequestCreateAccountJsonBuilder WithNumber(string? number)
    {
        _number = number;
        return this;
    }

    public RequestCreateAccountJsonBuilder WithTabAccountId(Guid? tabAccountId)
    {
        _tabAccountId = tabAccountId;
        return this;
    }

    public RequestCreateAccountJson Build()
    {
        return new RequestCreateAccountJson
        {
            Type = _type,
            Name = _name,
            Institution = _institution,
            Number = _number,
            TabAccountId = _tabAccountId
        };
    }
}
