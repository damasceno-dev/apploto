using server.Communication.Responses;

namespace server.Application.Services.Reports;

internal static class PostedReportingContext
{
    public static ResponsePostedReportingContextJson ForMonth(int year, int month, DateTime asOfDate)
    {
        var (from, to, _) = MonthlyFinancialReader.MonthWindow(year, month, asOfDate);
        return new ResponsePostedReportingContextJson
        {
            Year = year, Month = month, AsOfDate = asOfDate, DateFrom = from, DateTo = to, BankPositionComplete = false
        };
    }
}
