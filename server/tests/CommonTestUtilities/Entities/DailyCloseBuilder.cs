using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Operator = server.Domain.Entities.Operator;

namespace CommonTestUtilities.Entities;

public class DailyCloseBuilder
{
    private Guid _id = Guid.NewGuid();
    private uint _version = 1;
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _active = true;
    private DateTime _date = DateTime.Today;
    private DailyCloseStatus _status = DailyCloseStatus.Draft;
    private Guid _accountId = Guid.NewGuid();
    private Account? _account;
    private Guid? _submittedByOperatorId;
    private Operator? _submittedByOperator;
    private DateTime? _submittedAt;
    private DateTime? _approvedAt;
    private Guid? _approvedByUserId;
    private User? _approvedByUser;
    private string? _rejectionReason;
    private string? _notes;
    private DateTime? _updatedAt;
    private Guid? _updatedByUserId;
    private Guid _branchId = Guid.NewGuid();
    private Branch? _branch;
    private IReadOnlyList<DailyCloseItem> _items = [];

    public DailyCloseBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public DailyCloseBuilder WithVersion(uint version)
    {
        _version = version;
        return this;
    }

    public DailyCloseBuilder WithCreatedAt(DateTime createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public DailyCloseBuilder WithActive(bool active)
    {
        _active = active;
        return this;
    }

    public DailyCloseBuilder WithStatus(DailyCloseStatus status)
    {
        _status = status;
        return this;
    }

    public DailyCloseBuilder WithDate(DateTime date)
    {
        _date = date;
        return this;
    }

    public DailyCloseBuilder WithAccount(Account account)
    {
        _account = account;
        _accountId = account.Id;
        _branchId = account.BranchId;
        _branch = account.Branch;
        return this;
    }

    public DailyCloseBuilder WithAccountId(Guid accountId)
    {
        _accountId = accountId;
        _account = null;
        return this;
    }

    public DailyCloseBuilder WithBranchId(Guid branchId)
    {
        _branchId = branchId;
        _branch = null;
        return this;
    }

    public DailyCloseBuilder WithBranch(Branch branch)
    {
        _branch = branch;
        _branchId = branch.Id;
        return this;
    }

    public DailyCloseBuilder WithSubmittedByOperator(Operator? submittedByOperator)
    {
        _submittedByOperator = submittedByOperator;
        _submittedByOperatorId = submittedByOperator?.Id;
        return this;
    }

    public DailyCloseBuilder WithSubmittedAt(DateTime? submittedAt)
    {
        _submittedAt = submittedAt;
        return this;
    }

    public DailyCloseBuilder WithApprovedByUser(User? approvedByUser)
    {
        _approvedByUser = approvedByUser;
        _approvedByUserId = approvedByUser?.Id;
        return this;
    }

    public DailyCloseBuilder WithApprovedAt(DateTime? approvedAt)
    {
        _approvedAt = approvedAt;
        return this;
    }

    public DailyCloseBuilder WithRejectionReason(string? rejectionReason)
    {
        _rejectionReason = rejectionReason;
        return this;
    }

    public DailyCloseBuilder WithNotes(string? notes)
    {
        _notes = notes;
        return this;
    }

    public DailyCloseBuilder WithItems(IReadOnlyList<DailyCloseItem> items)
    {
        _items = items;
        return this;
    }

    public DailyCloseBuilder WithUpdated(DateTime updatedAt, Guid updatedByUserId)
    {
        _updatedAt = updatedAt;
        _updatedByUserId = updatedByUserId;
        return this;
    }

    public DailyClose Build()
    {
        var branch = _branch ?? new BranchBuilder().WithId(_branchId).Build();
        var account = _account ?? new AccountBuilder()
            .WithId(_accountId)
            .WithBranch(branch)
            .Build();

        return new DailyClose
        {
            Id = _id,
            Version = _version,
            CreatedAt = _createdAt,
            Active = _active,
            Date = _date,
            Status = _status,
            AccountId = _accountId,
            Account = account,
            SubmittedByOperatorId = _submittedByOperatorId,
            SubmittedByOperator = _submittedByOperator,
            SubmittedAt = _submittedAt,
            ApprovedAt = _approvedAt,
            ApprovedByUserId = _approvedByUserId,
            ApprovedByUser = _approvedByUser,
            RejectionReason = _rejectionReason,
            Notes = _notes,
            UpdatedAt = _updatedAt,
            UpdatedByUserId = _updatedByUserId,
            BranchId = _branchId,
            Branch = branch,
            Items = _items.ToList()
        };
    }
}
