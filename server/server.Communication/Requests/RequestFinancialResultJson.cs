using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestFinancialResultJson
{
    public int Year { get; init; }
    public int Month { get; init; }
    public DateTime? AsOfDate { get; init; }
    public FinancialClass? FinancialClass { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}
