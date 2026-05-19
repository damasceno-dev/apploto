using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUpdateCategoryJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private string _name;

    public RequestUpdateCategoryJsonBuilder()
    {
        _name = _faker.Commerce.Department();
    }

    public RequestUpdateCategoryJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestUpdateCategoryJson Build()
    {
        return new RequestUpdateCategoryJson
        {
            Name = _name
        };
    }
}
