using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestListDailyClosesJsonBuilder
{
    private Guid? _accountId;
    private DailyCloseStatus? _status;
    private DateTime? _dateFrom;
    private DateTime? _dateTo;
    private Guid? _operatorId;
    private bool _mine;
    private int _page = 1;
    private int _pageSize = 20;

    public RequestListDailyClosesJsonBuilder WithAccountId(Guid? accountId)
    {
        _accountId = accountId;
        return this;
    }

    public RequestListDailyClosesJsonBuilder WithStatus(DailyCloseStatus? status)
    {
        _status = status;
        return this;
    }

    public RequestListDailyClosesJsonBuilder WithDateFrom(DateTime? dateFrom)
    {
        _dateFrom = dateFrom;
        return this;
    }

    public RequestListDailyClosesJsonBuilder WithDateTo(DateTime? dateTo)
    {
        _dateTo = dateTo;
        return this;
    }

    public RequestListDailyClosesJsonBuilder WithOperatorId(Guid? operatorId)
    {
        _operatorId = operatorId;
        return this;
    }

    public RequestListDailyClosesJsonBuilder WithMine(bool mine)
    {
        _mine = mine;
        return this;
    }

    public RequestListDailyClosesJsonBuilder WithPage(int page)
    {
        _page = page;
        return this;
    }

    public RequestListDailyClosesJsonBuilder WithPageSize(int pageSize)
    {
        _pageSize = pageSize;
        return this;
    }

    public RequestListDailyClosesJson Build()
    {
        return new RequestListDailyClosesJson
        {
            AccountId = _accountId,
            Status = _status,
            DateFrom = _dateFrom,
            DateTo = _dateTo,
            OperatorId = _operatorId,
            Mine = _mine,
            Page = _page,
            PageSize = _pageSize
        };
    }
}
