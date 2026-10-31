namespace server.Exceptions.Exceptions;

/// <summary>
/// An all-or-nothing batch refused because at least one item failed: every item's message (already formatted with the
/// item's identity) and the status of the first failure in request order.
/// </summary>
public class BatchRejectedException(int statusCode, List<string> errorMessages)
    : ServerException(string.Join(" ", errorMessages))
{
    public override int GetStatusCode => statusCode;

    public override List<string> GetErrorMessages => errorMessages;
}
