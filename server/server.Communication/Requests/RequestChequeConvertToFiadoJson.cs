namespace server.Communication.Requests;

/// <summary>
/// <c>POST /cheque/{transactionId}/convert-to-fiado</c> — <i>Converter em fiado</i> (M9.7 Phase 2.3, §6.19 O21): in one
/// commit the cheque claim closes and the identified client's fiado debt of the same amount opens on the Tab; no
/// cashbox leg and no counter-sale pairing.
/// </summary>
public class RequestChequeConvertToFiadoJson
{
    public int ExpectedVersion { get; init; }
    public Guid ClientId { get; init; }
    public Guid TabAccountId { get; init; }
    public DateTime OccurredOn { get; init; }

    /// <summary>The open business date of the conversion and of the fiado debt row; omitted means branch-local today.</summary>
    public DateTime? PostedOn { get; init; }

    public string? Description { get; init; }
}
