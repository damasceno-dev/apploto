using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUpdateOperatorJsonBuilder
{
    private readonly Faker _faker = new();
    private string _name;
    private Guid? _userId;

    public RequestUpdateOperatorJsonBuilder()
    {
        _name = _faker.Name.FullName();
    }

    public RequestUpdateOperatorJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestUpdateOperatorJsonBuilder WithUserId(Guid? userId)
    {
        _userId = userId;
        return this;
    }

    public RequestUpdateOperatorJson Build()
    {
        return new RequestUpdateOperatorJson
        {
            Name = _name,
            UserId = _userId
        };
    }
}
