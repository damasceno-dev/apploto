using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateBankAccountJsonBuilder
{
    private readonly Faker _faker = new();
    private string _name;
    private string? _institution;
    private string? _number;

    public RequestCreateBankAccountJsonBuilder()
    {
        _name = _faker.Commerce.ProductName();
    }

    public RequestCreateBankAccountJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestCreateBankAccountJsonBuilder WithInstitution(string? institution)
    {
        _institution = institution;
        return this;
    }

    public RequestCreateBankAccountJsonBuilder WithNumber(string? number)
    {
        _number = number;
        return this;
    }

    public RequestCreateBankAccountJson Build()
    {
        return new RequestCreateBankAccountJson
        {
            Name = _name,
            Institution = _institution,
            Number = _number
        };
    }
}
