using server.Communication.Requests;

namespace CommonTestUtilities.Requests;

public class RequestMonthlyReconciliationJsonBuilder
{
    private int _year = 2025;
    private int _month = 1;

    public RequestMonthlyReconciliationJsonBuilder WithYear(int year)
    {
        _year = year;
        return this;
    }

    public RequestMonthlyReconciliationJsonBuilder WithMonth(int month)
    {
        _month = month;
        return this;
    }

    public RequestMonthlyReconciliationJson Build()
    {
        return new RequestMonthlyReconciliationJson
        {
            Year = _year,
            Month = _month
        };
    }
}
