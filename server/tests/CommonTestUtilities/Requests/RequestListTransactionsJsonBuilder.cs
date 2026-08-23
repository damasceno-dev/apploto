using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestListTransactionsJsonBuilder
{
    private Guid? _accountId;
    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private TransactionStatus? _status;
    private Guid? _operatorId;
    private Guid? _clientId;
    private Guid? _originTransactionId;
    private int _page = 1;
    private int _pageSize = 50;
    private bool _mine;

    public RequestListTransactionsJsonBuilder WithAccountId(Guid? accountId)
    {
        _accountId = accountId;
        return this;
    }

    public RequestListTransactionsJsonBuilder WithDateFrom(DateTime? dateFrom)
    {
        _dateFrom = dateFrom;
        return this;
    }

    public RequestListTransactionsJsonBuilder WithDateTo(DateTime? dateTo)
    {
        _dateTo = dateTo;
        return this;
    }

    public RequestListTransactionsJsonBuilder WithStatus(TransactionStatus? status)
    {
        _status = status;
        return this;
    }

    public RequestListTransactionsJsonBuilder WithOperatorId(Guid? operatorId)
    {
        _operatorId = operatorId;
        return this;
    }

    public RequestListTransactionsJsonBuilder WithClientId(Guid? clientId)
    {
        _clientId = clientId;
        return this;
    }

    public RequestListTransactionsJsonBuilder WithOriginTransactionId(Guid? originTransactionId)
    {
        _originTransactionId = originTransactionId;
        return this;
    }

    public RequestListTransactionsJsonBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public RequestListTransactionsJsonBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public RequestListTransactionsJsonBuilder WithMine(bool mine)
    {
        _mine = mine;
        return this;
    }

    public RequestListTransactionsJson Build()
    {
        return new RequestListTransactionsJson
        {
            AccountId = _accountId,
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            Status = _status,
            OperatorId = _operatorId,
            ClientId = _clientId,
            OriginTransactionId = _originTransactionId,
            Page = _page,
            PageSize = _pageSize,
            Mine = _mine
        };
    }
}
