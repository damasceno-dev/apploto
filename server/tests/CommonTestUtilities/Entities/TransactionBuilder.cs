using Bogus;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Entities;

public class TransactionBuilder
{
    private readonly Faker _faker = new("pt_BR");
    private Guid _id = Guid.NewGuid();
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _active = true;
    private uint _version;
    private DateTime _date;
    private decimal _value;
    private string? _description;
    private TimeOnly? _transactionTime;
    private Guid _transactionTypeId = Guid.NewGuid();
    private TransactionType? _transactionType;
    private Guid _categoryId = Guid.NewGuid();
    private Category? _category;
    private Direction _direction = Direction.In;
    private Guid _accountId = Guid.NewGuid();
    private Account? _account;
    private Guid? _clientId;
    private Client? _client;
    private DateTime _dueDate;
    private DateTime? _paidAt;
    private Guid? _originTransactionId;
    private Transaction? _originTransaction;
    private Guid _recordedByOperatorId = Guid.NewGuid();
    private Operator? _recordedByOperator;
    private Guid _createdByUserId = Guid.NewGuid();
    private User? _createdByUser;
    private DateTime? _updatedAt;
    private Guid? _updatedByUserId;
    private TransactionStatus _status = TransactionStatus.Active;
    private DateTime? _cancelledAt;
    private Guid? _cancelledByUserId;
    private User? _cancelledByUser;
    private string? _cancellationReason;
    private Guid _branchId = Guid.NewGuid();
    private Branch? _branch;

    public TransactionBuilder()
    {
        _date = _faker.Date.RecentDateOnly(30).ToDateTime(TimeOnly.MinValue);
        _dueDate = _date;
        _value = Math.Round(_faker.Random.Decimal(1m, 999_999.99m), 2);
        _description = _faker.Commerce.ProductDescription();
        _transactionTime = TimeOnly.FromDateTime(_faker.Date.Recent());
    }

    public static TransactionBuilder From(Transaction existing)
    {
        return new TransactionBuilder
        {
            _id = existing.Id,
            _createdAt = existing.CreatedAt,
            _active = existing.Active,
            _version = existing.Version,
            _date = existing.Date,
            _value = existing.Value,
            _description = existing.Description,
            _transactionTime = existing.TransactionTime,
            _transactionTypeId = existing.TransactionTypeId,
            _transactionType = existing.TransactionType,
            _categoryId = existing.CategoryId,
            _category = existing.Category,
            _direction = existing.Direction,
            _accountId = existing.AccountId,
            _account = existing.Account,
            _clientId = existing.ClientId,
            _client = existing.Client,
            _dueDate = existing.DueDate,
            _paidAt = existing.PaidAt,
            _originTransactionId = existing.OriginTransactionId,
            _originTransaction = existing.OriginTransaction,
            _recordedByOperatorId = existing.RecordedByOperatorId,
            _recordedByOperator = existing.RecordedByOperator,
            _createdByUserId = existing.CreatedByUserId,
            _createdByUser = existing.CreatedByUser,
            _updatedAt = existing.UpdatedAt,
            _updatedByUserId = existing.UpdatedByUserId,
            _status = existing.Status,
            _cancelledAt = existing.CancelledAt,
            _cancelledByUserId = existing.CancelledByUserId,
            _cancelledByUser = existing.CancelledByUser,
            _cancellationReason = existing.CancellationReason,
            _branchId = existing.BranchId,
            _branch = existing.Branch
        };
    }

    public TransactionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TransactionBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public TransactionBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public TransactionBuilder WithVersion(uint version)
    {
        _version = version;
        return this;
    }

    public TransactionBuilder WithValue(decimal value)
    {
        _value = value;
        return this;
    }

    public TransactionBuilder WithStatus(TransactionStatus status)
    {
        _status = status;
        return this;
    }

    public TransactionBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        _branch = null;
        return this;
    }

    public TransactionBuilder WithBranch(Branch branch)
    {
        _branch = branch;
        _branchId = branch.Id;
        return this;
    }

    public TransactionBuilder WithAccount(Account account)
    {
        _account = account;
        _accountId = account.Id;
        return this;
    }

    public TransactionBuilder WithAccountId(Guid accountId)
    {
        _accountId = accountId;
        _account = null;
        return this;
    }

    public TransactionBuilder WithRecordedByOperator(Operator op)
    {
        _recordedByOperator = op;
        _recordedByOperatorId = op.Id;
        return this;
    }

    public TransactionBuilder WithRecordedByOperatorId(Guid operatorId)
    {
        _recordedByOperatorId = operatorId;
        _recordedByOperator = null;
        return this;
    }

    public TransactionBuilder WithCreatedByUser(User user)
    {
        _createdByUser = user;
        _createdByUserId = user.Id;
        return this;
    }

    public TransactionBuilder WithCreatedByUserId(Guid userId)
    {
        _createdByUserId = userId;
        _createdByUser = null;
        return this;
    }

    public TransactionBuilder WithUpdatedAt(DateTime? updatedAt)
    {
        _updatedAt = updatedAt;
        return this;
    }

    public TransactionBuilder WithUpdatedByUserId(Guid? userId)
    {
        _updatedByUserId = userId;
        return this;
    }

    public TransactionBuilder WithTransactionType(TransactionType transactionType)
    {
        _transactionType = transactionType;
        _transactionTypeId = transactionType.Id;
        _categoryId = transactionType.CategoryId;
        _category = transactionType.Category;
        _direction = transactionType.Category?.DefaultDirection ?? _direction;
        return this;
    }

    public TransactionBuilder WithTransactionTypeId(Guid transactionTypeId)
    {
        _transactionTypeId = transactionTypeId;
        _transactionType = null;
        return this;
    }

    public TransactionBuilder WithClient(Client? client)
    {
        _client = client;
        _clientId = client?.Id;
        return this;
    }

    public TransactionBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        _client = null;
        return this;
    }

    public TransactionBuilder WithDueDate(DateTime dueDate)
    {
        _dueDate = dueDate;
        return this;
    }

    public TransactionBuilder WithPaidAt(DateTime? paidAt)
    {
        _paidAt = paidAt;
        return this;
    }

    public TransactionBuilder WithOriginTransactionId(Guid? originTransactionId)
    {
        _originTransactionId = originTransactionId;
        _originTransaction = null;
        return this;
    }

    public TransactionBuilder WithOriginTransaction(Transaction? originTransaction)
    {
        _originTransaction = originTransaction;
        _originTransactionId = originTransaction?.Id;
        return this;
    }

    public TransactionBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public TransactionBuilder WithTransactionTime(DateTime? transactionTime)
    {
        _transactionTime = transactionTime.HasValue
            ? TimeOnly.FromDateTime(transactionTime.Value)
            : null;
        return this;
    }

    public TransactionBuilder WithTransactionTime(TimeOnly? transactionTime)
    {
        _transactionTime = transactionTime;
        return this;
    }

    public TransactionBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public TransactionBuilder WithCancelledAt(DateTime? cancelledAt)
    {
        _cancelledAt = cancelledAt;
        return this;
    }

    public TransactionBuilder WithCancelledByUser(User? user)
    {
        _cancelledByUser = user;
        _cancelledByUserId = user?.Id;
        return this;
    }

    public TransactionBuilder WithCancelledByUserId(Guid? userId)
    {
        _cancelledByUserId = userId;
        _cancelledByUser = null;
        return this;
    }

    public TransactionBuilder WithCancellationReason(string? reason)
    {
        _cancellationReason = reason;
        return this;
    }

    public Transaction Build()
    {
        var branch = _branch ?? new BranchBuilder().WithId(_branchId).Build();
        var account = _account ?? new AccountBuilder().WithId(_accountId).WithBranch(branch).Build();
        var category = _category ?? new Category
        {
            Id = _categoryId,
            Name = _faker.Commerce.Categories(1)[0],
            DefaultDirection = _direction,
            BranchId = _branchId,
            Branch = branch
        };
        var transactionType = _transactionType ?? new TransactionTypeBuilder()
            .WithId(_transactionTypeId)
            .WithCategory(category)
            .Build();
        var recordedByOperator = _recordedByOperator ?? new OperatorBuilder()
            .WithId(_recordedByOperatorId)
            .WithBranch(branch)
            .Build();
        var createdByUser = _createdByUser ?? new UserBuilder().WithId(_createdByUserId).Build();

        return new Transaction
        {
            Id = _id,
            CreatedAt = _createdAt,
            Active = _active,
            Version = _version,
            Date = _date,
            Value = _value,
            Description = _description,
            TransactionTime = _transactionTime,
            TransactionTypeId = _transactionTypeId,
            TransactionType = transactionType,
            CategoryId = _categoryId,
            Category = category,
            Direction = _direction,
            AccountId = _accountId,
            Account = account,
            ClientId = _clientId,
            Client = _client,
            DueDate = _dueDate,
            PaidAt = _paidAt,
            OriginTransactionId = _originTransactionId,
            OriginTransaction = _originTransaction,
            RecordedByOperatorId = _recordedByOperatorId,
            RecordedByOperator = recordedByOperator,
            CreatedByUserId = _createdByUserId,
            CreatedByUser = createdByUser,
            UpdatedAt = _updatedAt,
            UpdatedByUserId = _updatedByUserId,
            Status = _status,
            CancelledAt = _cancelledAt,
            CancelledByUserId = _cancelledByUserId,
            CancelledByUser = _cancelledByUser,
            CancellationReason = _cancellationReason,
            BranchId = _branchId,
            Branch = branch
        };
    }
}
