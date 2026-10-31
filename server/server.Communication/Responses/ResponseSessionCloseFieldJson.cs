using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// The current value of one close-form field of a session (M9.6 Phase 7) and the generated row that expresses it.
/// Every field is listed: a field never set reads <c>0</c> at revision <c>0</c>.
/// </summary>
public class ResponseSessionCloseFieldJson
{
    public SessionCloseField Field { get; init; }

    /// <summary>Signed value; only <see cref="SessionCloseField.TerminalReport"/> may be negative.</summary>
    public decimal Value { get; init; }
    public int Revision { get; init; }

    /// <summary>The Active generated row (<c>In</c>, or <c>Out</c> for a negative report); null at zero.</summary>
    public Guid? TransactionId { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public Guid? UpdatedByUserId { get; init; }
}
