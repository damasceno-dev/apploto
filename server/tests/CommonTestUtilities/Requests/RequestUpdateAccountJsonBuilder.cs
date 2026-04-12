using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUpdateAccountJsonBuilder
{
    private readonly Faker _faker = new();
    private string _name;
    private string? _institution;
    private string? _number;
    private Guid? _tabAccountId;

    public RequestUpdateAccountJsonBuilder()
    {
        _name = _faker.Commerce.ProductName();
    }

    public RequestUpdateAccountJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestUpdateAccountJsonBuilder WithInstitution(string? institution)
    {
        _institution = institution;
        return this;
    }

    public RequestUpdateAccountJsonBuilder WithNumber(string? number)
    {
        _number = number;
        return this;
    }

    public RequestUpdateAccountJsonBuilder WithTabAccountId(Guid? tabAccountId)
    {
        _tabAccountId = tabAccountId;
        return this;
    }

    public RequestUpdateAccountJson Build()
    {
        return new RequestUpdateAccountJson
        {
            Name = _name,
            Institution = _institution,
            Number = _number,
            TabAccountId = _tabAccountId
        };
    }
}
