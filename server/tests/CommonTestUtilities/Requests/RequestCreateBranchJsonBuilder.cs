using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateBranchJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private string _name;
    private string? _cnpj = "12.345.678/0001-90";
    private string? _address;
    private string? _phone;

    public RequestCreateBranchJsonBuilder()
    {
        _name = _faker.Company.CompanyName();
        _address = _faker.Address.FullAddress();
        _phone = _faker.Phone.PhoneNumber();
    }

    public RequestCreateBranchJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestCreateBranchJsonBuilder WithCnpj(string? cnpj)
    {
        _cnpj = cnpj;
        return this;
    }

    public RequestCreateBranchJsonBuilder WithAddress(string? address)
    {
        _address = address;
        return this;
    }

    public RequestCreateBranchJsonBuilder WithPhone(string? phone)
    {
        _phone = phone;
        return this;
    }

    public RequestCreateBranchJson Build()
    {
        return new RequestCreateBranchJson
        {
            Name = _name,
            Cnpj = _cnpj,
            Address = _address,
            Phone = _phone
        };
    }
}
