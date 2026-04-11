using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUserLoginJsonBuilder
{
    private readonly Faker _faker = new();
    private string _email;
    private string _password;

    public RequestUserLoginJsonBuilder()
    {
        _email = _faker.Internet.Email();
        _password = "Password123";
    }

    public RequestUserLoginJsonBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public RequestUserLoginJsonBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public RequestUserLoginJson Build()
    {
        return new RequestUserLoginJson
        {
            Email = _email,
            Password = _password
        };
    }
}
