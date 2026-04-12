using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateOperatorJsonBuilder
{
    private readonly Faker _faker = new();
    private string _name;
    private Guid? _userId;

    public RequestCreateOperatorJsonBuilder()
    {
        _name = _faker.Name.FullName();
    }

    public RequestCreateOperatorJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestCreateOperatorJsonBuilder WithUserId(Guid? userId)
    {
        _userId = userId;
        return this;
    }

    public RequestCreateOperatorJson Build()
    {
        return new RequestCreateOperatorJson
        {
            Name = _name,
            UserId = _userId
        };
    }
}
