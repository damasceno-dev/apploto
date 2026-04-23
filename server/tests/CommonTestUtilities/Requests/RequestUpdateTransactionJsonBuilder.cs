using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestUpdateTransactionJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private string? _description;
    private DateTime _dueDate;
    private DateTime? _paidAt;
    private Guid? _clientId;
    private TimeOnly? _transactionTime;

    public RequestUpdateTransactionJsonBuilder()
    {
        _description = _faker.Commerce.ProductDescription();
        _dueDate = _faker.Date.SoonDateOnly(30).ToDateTime(TimeOnly.MinValue);
        _paidAt = null;
        _clientId = null;
        _transactionTime = TimeOnly.FromDateTime(_faker.Date.Recent());
    }

    public RequestUpdateTransactionJsonBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public RequestUpdateTransactionJsonBuilder WithDueDate(DateTime dueDate)
    {
        _dueDate = dueDate;
        return this;
    }

    public RequestUpdateTransactionJsonBuilder WithPaidAt(DateTime? paidAt)
    {
        _paidAt = paidAt;
        return this;
    }

    public RequestUpdateTransactionJsonBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        return this;
    }

    public RequestUpdateTransactionJsonBuilder WithTransactionTime(TimeOnly? transactionTime)
    {
        _transactionTime = transactionTime;
        return this;
    }

    public RequestUpdateTransactionJson Build()
    {
        return new RequestUpdateTransactionJson
        {
            Description = _description,
            DueDate = _dueDate,
            PaidAt = _paidAt,
            ClientId = _clientId,
            TransactionTime = _transactionTime
        };
    }
}
