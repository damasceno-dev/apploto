using Bogus;
using CommonTestUtilities.Services;
using server.Domain.Entities;

namespace CommonTestUtilities.Entities;

public class UserBuilder
{
    private readonly Faker _faker = new();
    private Guid _id = Guid.NewGuid();
    private string _name;
    private string _email;
    private string _password = "Password123";
    private bool _hashPassword;
    private bool _active = true;

    public UserBuilder()
    {
        _name = _faker.Person.FirstName;
        _email = _faker.Internet.Email(firstName: _name);
    }

    public UserBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public UserBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public UserBuilder WithPassword(string password)
    {
        _password = password;
        _hashPassword = false;
        return this;
    }

    public UserBuilder WithHashedPassword(string password)
    {
        _password = password;
        _hashPassword = true;
        return this;
    }

    public UserBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public User Build()
    {
        var password = _hashPassword
            ? PasswordEncryptionBuilder.Build().HashPassword(_password)
            : _password;

        return new User
        {
            Id = _id,
            Name = _name,
            Email = _email,
            Password = password,
            Active = _active
        };
    }
}
