using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateTransactionJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private DateTime _date;
    private decimal _value;
    private string? _description;
    private TimeOnly? _transactionTime;
    private Guid _transactionTypeId = Guid.NewGuid();
    private Guid _accountId = Guid.NewGuid();
    private Guid? _clientId;
    private DateTime? _dueDate;
    private Guid? _recordedByOperatorId;
    private bool _saveAsDraft;

    public RequestCreateTransactionJsonBuilder()
    {
        _date = _faker.Date.RecentDateOnly(30).ToDateTime(TimeOnly.MinValue);
        _value = Math.Round(_faker.Random.Decimal(1m, 999_999.99m), 2);
        _description = _faker.Commerce.ProductDescription();
        _transactionTime = TimeOnly.FromDateTime(_faker.Date.Recent());
    }

    public RequestCreateTransactionJsonBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public RequestCreateTransactionJsonBuilder WithValue(decimal value)
    {
        _value = value;
        return this;
    }

    public RequestCreateTransactionJsonBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public RequestCreateTransactionJsonBuilder WithTransactionTime(TimeOnly? transactionTime)
    {
        _transactionTime = transactionTime;
        return this;
    }

    public RequestCreateTransactionJsonBuilder WithTransactionTypeId(Guid transactionTypeId)
    {
        _transactionTypeId = transactionTypeId;
        return this;
    }

    public RequestCreateTransactionJsonBuilder WithAccountId(Guid accountId)
    {
        _accountId = accountId;
        return this;
    }

    public RequestCreateTransactionJsonBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        return this;
    }

    public RequestCreateTransactionJsonBuilder WithDueDate(DateTime? dueDate)
    {
        _dueDate = dueDate;
        return this;
    }

    public RequestCreateTransactionJsonBuilder WithRecordedByOperatorId(Guid? recordedByOperatorId)
    {
        _recordedByOperatorId = recordedByOperatorId;
        return this;
    }

    public RequestCreateTransactionJsonBuilder WithSaveAsDraft(bool saveAsDraft)
    {
        _saveAsDraft = saveAsDraft;
        return this;
    }

    public RequestCreateTransactionJson Build()
    {
        return new RequestCreateTransactionJson
        {
            Date = _date,
            Value = _value,
            Description = _description,
            TransactionTime = _transactionTime,
            TransactionTypeId = _transactionTypeId,
            AccountId = _accountId,
            ClientId = _clientId,
            DueDate = _dueDate,
            RecordedByOperatorId = _recordedByOperatorId,
            SaveAsDraft = _saveAsDraft
        };
    }
}
