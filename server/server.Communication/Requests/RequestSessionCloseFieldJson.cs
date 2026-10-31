using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

/// <summary>
/// One close-form field on the count save (M9.6 Phase 7). A field that is not listed keeps its value; a listed field
/// with <c>0</c> cancels its effect; <c>Value</c> is required (null is refused). Fees are nonnegative; the terminal
/// report is signed. Every value fits numeric(14,2) with at most two decimal places and is never rounded.
/// </summary>
public class RequestSessionCloseFieldJson
{
    public SessionCloseField Field { get; init; }
    public decimal? Value { get; init; }
}
