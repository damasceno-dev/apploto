using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public sealed class RequestLockSettingMonthJsonBuilder
{
    private int _year = 2025;
    private int _month = 5;

    public RequestLockSettingMonthJsonBuilder WithYear(int year)
    {
        _year = year;
        return this;
    }

    public RequestLockSettingMonthJsonBuilder WithMonth(int month)
    {
        _month = month;
        return this;
    }

    public RequestLockSettingMonthJson Build()
    {
        return new RequestLockSettingMonthJson { Year = _year, Month = _month };
    }
}
