using Bogus;
using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class BranchBuilder
{
    private readonly Faker _faker = new();
    private Guid _id = Guid.NewGuid();
    private string _name;
    private string? _cnpj;
    private string? _address;
    private string? _phone;
    private bool _active = true;

    public BranchBuilder()
    {
        _name = $"Branch {_faker.Company.CompanyName()}";
        _address = _faker.Address.FullAddress();
        _phone = _faker.Phone.PhoneNumber();
    }

    public BranchBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public BranchBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public BranchBuilder WithCnpj(string? cnpj)
    {
        _cnpj = cnpj;
        return this;
    }

    public BranchBuilder WithAddress(string? address)
    {
        _address = address;
        return this;
    }

    public BranchBuilder WithPhone(string? phone)
    {
        _phone = phone;
        return this;
    }

    public BranchBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public Branch Build()
    {
        return new Branch
        {
            Id = _id,
            Name = _name,
            Cnpj = _cnpj,
            Address = _address,
            Phone = _phone,
            Active = _active
        };
    }
}
