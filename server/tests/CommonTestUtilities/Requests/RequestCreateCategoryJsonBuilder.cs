using Bogus;
using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestCreateCategoryJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private string _name;
    private Direction _defaultDirection = Direction.In;

    public RequestCreateCategoryJsonBuilder()
    {
        _name = _faker.Commerce.Department();
    }

    public RequestCreateCategoryJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestCreateCategoryJsonBuilder WithDefaultDirection(Direction direction)
    {
        _defaultDirection = direction;
        return this;
    }

    public RequestCreateCategoryJson Build()
    {
        return new RequestCreateCategoryJson
        {
            Name = _name,
            DefaultDirection = _defaultDirection
        };
    }
}
