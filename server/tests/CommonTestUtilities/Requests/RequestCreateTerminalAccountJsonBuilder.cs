using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateTerminalAccountJsonBuilder
{
    private readonly Faker _faker = new();
    private string _name;
    private string? _institution;
    private string? _number;
    private Guid? _existingTabAccountId;
    private bool _createTabAccount;

    public RequestCreateTerminalAccountJsonBuilder()
    {
        _name = _faker.Commerce.ProductName();
    }

    public RequestCreateTerminalAccountJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestCreateTerminalAccountJsonBuilder WithInstitution(string? institution)
    {
        _institution = institution;
        return this;
    }

    public RequestCreateTerminalAccountJsonBuilder WithNumber(string? number)
    {
        _number = number;
        return this;
    }

    public RequestCreateTerminalAccountJsonBuilder WithExistingTabAccountId(Guid? existingTabAccountId)
    {
        _existingTabAccountId = existingTabAccountId;
        return this;
    }

    public RequestCreateTerminalAccountJsonBuilder WithCreateTabAccount(bool createTabAccount)
    {
        _createTabAccount = createTabAccount;
        return this;
    }

    public RequestCreateTerminalAccountJson Build()
    {
        return new RequestCreateTerminalAccountJson
        {
            Name = _name,
            Institution = _institution,
            Number = _number,
            ExistingTabAccountId = _existingTabAccountId,
            CreateTabAccount = _createTabAccount
        };
    }
}
