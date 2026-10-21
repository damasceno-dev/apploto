using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace CommonTestUtilities.Requests;

public class RequestUpcomingSettlementsJsonBuilder
{
    private DateTime _dueDateFrom = new(2026, 10, 20);
    private DateTime _dueDateTo = new(2026, 11, 2);
    private Guid? _accountId;
    private SettlementInstrument? _instrument;
    private int _page = 1;
    private int _pageSize = 50;

    public RequestUpcomingSettlementsJsonBuilder WithDueDateFrom(DateTime value)
    {
        _dueDateFrom = value;
        return this;
    }

    public RequestUpcomingSettlementsJsonBuilder WithDueDateTo(DateTime value)
    {
        _dueDateTo = value;
        return this;
    }

    public RequestUpcomingSettlementsJsonBuilder WithAccountId(Guid? value)
    {
        _accountId = value;
        return this;
    }

    public RequestUpcomingSettlementsJsonBuilder WithInstrument(SettlementInstrument? value)
    {
        _instrument = value;
        return this;
    }

    public RequestUpcomingSettlementsJsonBuilder WithPage(int value)
    {
        _page = value;
        return this;
    }

    public RequestUpcomingSettlementsJsonBuilder WithPageSize(int value)
    {
        _pageSize = value;
        return this;
    }

    public RequestUpcomingSettlementsJson Build()
    {
        return new RequestUpcomingSettlementsJson
        {
            DueDateFrom = _dueDateFrom,
            DueDateTo = _dueDateTo,
            AccountId = _accountId,
            Instrument = _instrument,
            Page = _page,
            PageSize = _pageSize
        };
    }
}
