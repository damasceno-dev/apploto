namespace server.Communication.Responses;

/// <summary>
/// One product's opening before and after a change. A null value means no saved item, distinct from
/// a recorded zero; <see cref="Change"/> is the signed effect on the opening total, where a missing
/// item counts as zero.
/// </summary>
public class ResponseDailyCloseOpeningChangeItemJson
{
    public Guid ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal? BeforeValue { get; init; }
    public decimal? AfterValue { get; init; }
    public decimal Change { get; init; }
}
