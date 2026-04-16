using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateClientJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private string _name;
    private string _phone;
    private string? _cpf;
    private string? _cep;
    private string? _address;
    private string? _phoneSecondary;
    private string? _notes;
    private string? _email;

    public RequestCreateClientJsonBuilder()
    {
        var firstName = _faker.Name.FirstName();
        var lastName = _faker.Name.LastName();
        _name = $"{firstName} {lastName}";
        _phone = _faker.Random.ReplaceNumbers("###########");
        _cpf = _faker.Random.ReplaceNumbers("###.###.###-##");  // formatted; use case normalizes to 11 digits
        _cep = _faker.Random.ReplaceNumbers("########");
        _address = _faker.Address.StreetAddress();
        _phoneSecondary = _faker.Random.ReplaceNumbers("###########");
        _notes = _faker.Lorem.Sentence();
        _email = _faker.Internet.Email(firstName, lastName);
    }

    public RequestCreateClientJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestCreateClientJsonBuilder WithPhone(string phone)
    {
        _phone = phone;
        return this;
    }

    public RequestCreateClientJsonBuilder WithCpf(string? cpf)
    {
        _cpf = cpf;
        return this;
    }

    public RequestCreateClientJsonBuilder WithCep(string? cep)
    {
        _cep = cep;
        return this;
    }

    public RequestCreateClientJsonBuilder WithAddress(string? address)
    {
        _address = address;
        return this;
    }

    public RequestCreateClientJsonBuilder WithPhoneSecondary(string? phoneSecondary)
    {
        _phoneSecondary = phoneSecondary;
        return this;
    }

    public RequestCreateClientJsonBuilder WithNotes(string? notes)
    {
        _notes = notes;
        return this;
    }

    public RequestCreateClientJsonBuilder WithEmail(string? email)
    {
        _email = email;
        return this;
    }

    public RequestCreateClientJson Build()
    {
        return new RequestCreateClientJson
        {
            Name = _name,
            Phone = _phone,
            Cpf = _cpf,
            Cep = _cep,
            Address = _address,
            PhoneSecondary = _phoneSecondary,
            Notes = _notes,
            Email = _email
        };
    }
}
