using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestResolveCustodyExceptionJson
{
    public CustodyExceptionAction Action { get; init; }
    public string? Reason { get; init; }

    /// <summary>AcceptAndStart: the operator who takes the cashbox, and the vector the manager observed.</summary>
    public Guid? IncomingOperatorId { get; init; }
    public List<RequestSessionOpeningItemJson>? Items { get; init; }
    public bool ExplicitZero { get; init; }
}
