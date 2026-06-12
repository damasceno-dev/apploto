using server.Domain.Models;

namespace CommonTestUtilities.Filters;

public class OperatorTransactionSummaryQueryBuilder
{
    private Guid _operatorId = Guid.NewGuid();
    private DateTime _dateFrom = new DateTime(2025, 1, 1);
    private DateTime _dateTo = new DateTime(2025, 1, 31);
    private IReadOnlyList<Guid>? _allowedAccountIds;

    public OperatorTransactionSummaryQueryBuilder WithOperatorId(Guid operatorId)
    {
        _operatorId = operatorId;
        return this;
    }

    public OperatorTransactionSummaryQueryBuilder WithDateFrom(DateTime dateFrom)
    {
        _dateFrom = dateFrom;
        return this;
    }

    public OperatorTransactionSummaryQueryBuilder WithDateTo(DateTime dateTo)
    {
        _dateTo = dateTo;
        return this;
    }

    public OperatorTransactionSummaryQueryBuilder WithAllowedAccountIds(IReadOnlyList<Guid>? allowedAccountIds)
    {
        _allowedAccountIds = allowedAccountIds;
        return this;
    }

    public OperatorTransactionSummaryQuery Build()
    {
        return new OperatorTransactionSummaryQuery
        {
            OperatorId = _operatorId,
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            AllowedAccountIds = _allowedAccountIds
        };
    }
}
