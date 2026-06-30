using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestTimeEntryBalanceSummaryJsonBuilder
{
    private Guid? _operatorId;
    private DateTime _dateFrom = new(2025, 1, 1);
    private DateTime _dateTo = new(2025, 1, 31);
    private bool _mine;

    public RequestTimeEntryBalanceSummaryJsonBuilder WithOperatorId(Guid? operatorId)
    {
        _operatorId = operatorId;
        return this;
    }

    public RequestTimeEntryBalanceSummaryJsonBuilder WithDateFrom(DateTime dateFrom)
    {
        _dateFrom = dateFrom;
        return this;
    }

    public RequestTimeEntryBalanceSummaryJsonBuilder WithDateTo(DateTime dateTo)
    {
        _dateTo = dateTo;
        return this;
    }

    public RequestTimeEntryBalanceSummaryJsonBuilder WithMine(bool mine)
    {
        _mine = mine;
        return this;
    }

    public RequestTimeEntryBalanceSummaryJson Build()
    {
        return new RequestTimeEntryBalanceSummaryJson
        {
            OperatorId = _operatorId,
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            Mine = _mine
        };
    }
}
