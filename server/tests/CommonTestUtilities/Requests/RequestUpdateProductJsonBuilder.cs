using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUpdateProductJsonBuilder
{
    private string _name = new Faker().Commerce.ProductName();
    private int _displayOrder = new Faker().Random.Int(0, 10);

    public RequestUpdateProductJsonBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public RequestUpdateProductJsonBuilder WithDisplayOrder(int displayOrder)
    {
        _displayOrder = displayOrder;
        return this;
    }

    public RequestUpdateProductJson Build()
    {
        return new RequestUpdateProductJson
        {
            Name = _name,
            DisplayOrder = _displayOrder
        };
    }
}
