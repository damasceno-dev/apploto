using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUpsertDailyCloseItemJsonBuilder
{
    private Guid _productId = Guid.NewGuid();
    private decimal _value = 100m;

    public RequestUpsertDailyCloseItemJsonBuilder WithProductId(Guid productId)
    {
        _productId = productId;
        return this;
    }

    public RequestUpsertDailyCloseItemJsonBuilder WithValue(decimal value)
    {
        _value = value;
        return this;
    }

    public RequestUpsertDailyCloseItemJson Build()
    {
        return new RequestUpsertDailyCloseItemJson
        {
            ProductId = _productId,
            Value = _value
        };
    }
}
