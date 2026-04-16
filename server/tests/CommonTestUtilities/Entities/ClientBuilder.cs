using Bogus;
using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class ClientBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private Guid _id = Guid.NewGuid();
    private string _name;
    private string _phone;
    private string? _cpf;
    private string? _cep;
    private string? _address;
    private string? _phoneSecondary;
    private string? _notes;
    private string? _email;
    private Guid _branchId = Guid.NewGuid();
    private Branch? _branch;
    private bool _active = true;

    public ClientBuilder()
    {
        var firstName = _faker.Name.FirstName();
        var lastName = _faker.Name.LastName();
        _name = $"{firstName} {lastName}";
        _phone = _faker.Random.ReplaceNumbers("###########");
        _cpf = _faker.Random.ReplaceNumbers("###########");   // normalized: 11 digits
        _cep = _faker.Random.ReplaceNumbers("########");
        _address = _faker.Address.StreetAddress();
        _phoneSecondary = _faker.Random.ReplaceNumbers("###########");
        _notes = _faker.Lorem.Sentence();
        _email = _faker.Internet.Email(firstName, lastName);
    }

    public ClientBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ClientBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ClientBuilder WithPhone(string phone)
    {
        _phone = phone;
        return this;
    }

    public ClientBuilder WithCpf(string? cpf)
    {
        _cpf = cpf;
        return this;
    }

    public ClientBuilder WithCep(string? cep)
    {
        _cep = cep;
        return this;
    }

    public ClientBuilder WithAddress(string? address)
    {
        _address = address;
        return this;
    }

    public ClientBuilder WithPhoneSecondary(string? phoneSecondary)
    {
        _phoneSecondary = phoneSecondary;
        return this;
    }

    public ClientBuilder WithNotes(string? notes)
    {
        _notes = notes;
        return this;
    }

    public ClientBuilder WithEmail(string? email)
    {
        _email = email;
        return this;
    }

    public ClientBuilder WithBranch(Branch branch)
    {
        _branch = branch;
        _branchId = branch.Id;
        return this;
    }

    public ClientBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        return this;
    }

    public ClientBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public Client Build()
    {
        var branch = _branch ?? new BranchBuilder().WithId(_branchId).Build();

        return new Client
        {
            Id = _id,
            Name = _name,
            Phone = _phone,
            Cpf = _cpf,
            Cep = _cep,
            Address = _address,
            PhoneSecondary = _phoneSecondary,
            Notes = _notes,
            Email = _email,
            BranchId = _branchId,
            Branch = branch,
            Active = _active
        };
    }
}
