using Bogus;
using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestCreateTransactionInstallmentJsonBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private DateTime _date;
    private decimal _value;
    private string? _description;
    private TimeOnly? _transactionTime;
    private Guid _transactionTypeId = Guid.NewGuid();
    private Guid _accountId = Guid.NewGuid();
    private Guid? _clientId;
    private IReadOnlyList<RequestCreateTransactionInstallmentItemJson> _installments = [];
    private bool _autoGenerateInstallments;
    private DateTime? _dueDate;
    private int? _installmentCount;
    private Guid? _recordedByOperatorId;
    private bool _saveAsDraft;

    public RequestCreateTransactionInstallmentJsonBuilder()
    {
        _date = DateTime.Today;
        _value = 300m;
        _description = _faker.Commerce.ProductDescription();
        _transactionTime = TimeOnly.FromDateTime(_faker.Date.Recent());
        _installments =
        [
            new RequestCreateTransactionInstallmentItemJson
            {
                DueDate = DateTime.Today.AddDays(30),
                Value = 100m
            },
            new RequestCreateTransactionInstallmentItemJson
            {
                DueDate = DateTime.Today.AddDays(60),
                Value = 100m
            },
            new RequestCreateTransactionInstallmentItemJson
            {
                DueDate = DateTime.Today.AddDays(90),
                Value = 100m
            }
        ];
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithValue(decimal value)
    {
        _value = value;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithTransactionTime(TimeOnly? transactionTime)
    {
        _transactionTime = transactionTime;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithTransactionTypeId(Guid transactionTypeId)
    {
        _transactionTypeId = transactionTypeId;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithAccountId(Guid accountId)
    {
        _accountId = accountId;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithInstallments(
        IReadOnlyList<RequestCreateTransactionInstallmentItemJson> installments)
    {
        _installments = installments;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithDueDate(DateTime? dueDate)
    {
        _dueDate = dueDate;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithRecordedByOperatorId(Guid? recordedByOperatorId)
    {
        _recordedByOperatorId = recordedByOperatorId;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithSaveAsDraft(bool saveAsDraft)
    {
        _saveAsDraft = saveAsDraft;
        return this;
    }

    /// <summary>
    /// Flag-only path: turns on auto-generation but leaves <c>InstallmentCount</c>,
    /// <c>DueDate</c>, and the default manual <c>Installments</c> untouched. Use this for
    /// negative tests that exercise the validator's handling of inconsistent combinations
    /// (e.g. the flag is on while manual rows are also present).
    /// </summary>
    public RequestCreateTransactionInstallmentJsonBuilder WithAutoGenerationEnabled()
    {
        _autoGenerateInstallments = true;
        return this;
    }

    /// <summary>
    /// Full valid auto-generation config: turns on the flag, clears manual rows, and sets
    /// both <c>InstallmentCount</c> and the base <c>DueDate</c>. Use this for happy-path
    /// auto-generation scenarios.
    /// </summary>
    public RequestCreateTransactionInstallmentJsonBuilder WithAutoGeneration(
        int installmentCount,
        DateTime baseDueDate)
    {
        _autoGenerateInstallments = true;
        _installments = [];
        _installmentCount = installmentCount;
        _dueDate = baseDueDate;
        return this;
    }

    public RequestCreateTransactionInstallmentJsonBuilder WithInstallmentCount(int? installmentCount)
    {
        _installmentCount = installmentCount;
        return this;
    }

    public RequestCreateTransactionInstallmentJson Build()
    {
        return new RequestCreateTransactionInstallmentJson
        {
            Date = _date,
            Value = _value,
            Description = _description,
            TransactionTime = _transactionTime,
            TransactionTypeId = _transactionTypeId,
            AccountId = _accountId,
            ClientId = _clientId,
            Installments = _installments,
            AutoGenerateInstallments = _autoGenerateInstallments,
            DueDate = _dueDate,
            InstallmentCount = _installmentCount,
            RecordedByOperatorId = _recordedByOperatorId,
            SaveAsDraft = _saveAsDraft
        };
    }
}
