using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUserRegisterJsonBuilder
{
    private readonly Faker _faker = new();
    private string _name;
    private string _email;
    private string _password;

    public RequestUserRegisterJsonBuilder()
    {
        _name = _faker.Person.FirstName;
        _email = _faker.Internet.Email(firstName: _name);
        _password = "Password123";
    }

    public RequestUserRegisterJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestUserRegisterJsonBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public RequestUserRegisterJsonBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public RequestUserRegisterJson Build()
    {
        return new RequestUserRegisterJson
        {
            Name = _name,
            Email = _email,
            Password = _password
        };
    }
}
