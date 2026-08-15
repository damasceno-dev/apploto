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
    private Guid _openedByUserId = Guid.NewGuid();
    private User? _openedByUser;
    private Guid? _recordedByUserId;
    private User? _recordedByUser;
    private Guid? _recordedByOperatorId;
    private Operator? _recordedByOperator;
    private Guid? _submittedByUserId;
    private User? _submittedByUser;
    private Guid? _submittedByOperatorId;
    private Operator? _submittedByOperator;
    private DateTime? _submittedAt;
    private DateTime? _approvedAt;
    private Guid? _approvedByUserId;
    private User? _approvedByUser;
    private string? _rejectionReason;
    private string? _notes;
    private DateTime? _itemsFirstRecordedAt;
    private DateTime? _openingRecheckRequiredAt;
    private Guid? _openingRecheckTriggeredByDailyCloseId;
    private DailyClose? _openingRecheckTriggeredByDailyClose;
    private Guid? _openingRecheckTriggeredByUserId;
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

    public DailyCloseBuilder WithOpenedByUser(User openedByUser)
    {
        _openedByUser = openedByUser;
        _openedByUserId = openedByUser.Id;
        return this;
    }

    public DailyCloseBuilder WithRecordedBy(User recordedByUser, Operator? recordedByOperator = null)
    {
        _recordedByUser = recordedByUser;
        _recordedByUserId = recordedByUser.Id;
        _recordedByOperator = recordedByOperator;
        _recordedByOperatorId = recordedByOperator?.Id;
        return this;
    }

    public DailyCloseBuilder WithSubmittedBy(User? submittedByUser, Operator? submittedByOperator = null)
    {
        _submittedByUser = submittedByUser;
        _submittedByUserId = submittedByUser?.Id;
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

    public DailyCloseBuilder WithItemsFirstRecordedAt(DateTime? itemsFirstRecordedAt)
    {
        _itemsFirstRecordedAt = itemsFirstRecordedAt;
        return this;
    }

    public DailyCloseBuilder WithOpeningRecheck(
        DateTime? requiredAt,
        DailyClose? triggeredByDailyClose,
        Guid? triggeredByUserId)
    {
        _openingRecheckRequiredAt = requiredAt;
        _openingRecheckTriggeredByDailyClose = triggeredByDailyClose;
        _openingRecheckTriggeredByDailyCloseId = triggeredByDailyClose?.Id;
        _openingRecheckTriggeredByUserId = triggeredByUserId;
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
        var openedByUser = _openedByUser ?? new UserBuilder().WithId(_openedByUserId).Build();
        var recordedByUser = _recordedByUser;
        if (_itemsFirstRecordedAt is not null && recordedByUser is null)
        {
            recordedByUser = _recordedByOperator?.User
                ?? new UserBuilder().WithId(_recordedByUserId ?? _recordedByOperator?.UserId ?? openedByUser.Id).Build();
            _recordedByUserId = recordedByUser.Id;
        }

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
            OpenedByUserId = _openedByUserId,
            OpenedByUser = openedByUser,
            RecordedByUserId = _recordedByUserId,
            RecordedByUser = recordedByUser,
            RecordedByOperatorId = _recordedByOperatorId,
            RecordedByOperator = _recordedByOperator,
            SubmittedByUserId = _submittedByUserId,
            SubmittedByUser = _submittedByUser,
            SubmittedByOperatorId = _submittedByOperatorId,
            SubmittedByOperator = _submittedByOperator,
            SubmittedAt = _submittedAt,
            ApprovedAt = _approvedAt,
            ApprovedByUserId = _approvedByUserId,
            ApprovedByUser = _approvedByUser,
            RejectionReason = _rejectionReason,
            Notes = _notes,
            ItemsFirstRecordedAt = _itemsFirstRecordedAt,
            OpeningRecheckRequiredAt = _openingRecheckRequiredAt,
            OpeningRecheckTriggeredByDailyCloseId = _openingRecheckTriggeredByDailyCloseId,
            OpeningRecheckTriggeredByDailyClose = _openingRecheckTriggeredByDailyClose,
            OpeningRecheckTriggeredByUserId = _openingRecheckTriggeredByUserId,
            UpdatedAt = _updatedAt,
            UpdatedByUserId = _updatedByUserId,
            BranchId = _branchId,
            Branch = branch,
            Items = _items.ToList()
        };
    }
}
