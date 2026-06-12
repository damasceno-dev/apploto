using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestOperatorTransactionSummaryJsonBuilder
{
    private Guid? _operatorId;
    private DateTime _dateFrom = new DateTime(2025, 1, 1);
    private DateTime _dateTo = new DateTime(2025, 1, 31);
    private bool _mine;

    public RequestOperatorTransactionSummaryJsonBuilder WithOperatorId(Guid? operatorId)
    {
        _operatorId = operatorId;
        return this;
    }

    public RequestOperatorTransactionSummaryJsonBuilder WithDateFrom(DateTime dateFrom)
    {
        _dateFrom = dateFrom;
        return this;
    }

    public RequestOperatorTransactionSummaryJsonBuilder WithDateTo(DateTime dateTo)
    {
        _dateTo = dateTo;
        return this;
    }

    public RequestOperatorTransactionSummaryJsonBuilder WithMine(bool mine)
    {
        _mine = mine;
        return this;
    }

    public RequestOperatorTransactionSummaryJson Build()
    {
        return new RequestOperatorTransactionSummaryJson
        {
            OperatorId = _operatorId,
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            Mine = _mine
        };
    }
}
