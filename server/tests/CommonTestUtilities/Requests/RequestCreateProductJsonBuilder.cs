using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateProductJsonBuilder
{
    private string _name = new Faker().Commerce.ProductName();
    private int _displayOrder = new Faker().Random.Int(0, 10);

    public RequestCreateProductJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestCreateProductJsonBuilder WithDisplayOrder(int displayOrder)
    {
        _displayOrder = displayOrder;
        return this;
    }

    public RequestCreateProductJson Build()
    {
        return new RequestCreateProductJson
        {
            Name = _name,
            DisplayOrder = _displayOrder
        };
    }
}
