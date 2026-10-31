namespace server.Communication.Requests;

public class RequestPutDailyCloseItemsJson
{
    public IReadOnlyList<RequestUpsertDailyCloseItemJson>? Items { get; init; }
    public string? Notes { get; init; }

    /// <summary>
    /// M9.6 Phase 7: the session's close-form fields to change (<i>Relatório do terminal</i>, <i>Tarifa Bolão</i>,
    /// <i>Tarifa Especial</i>), each at most once. Omitted (or an empty list) changes no field.
    /// </summary>
    public IReadOnlyList<RequestSessionCloseFieldJson>? CloseFields { get; init; }
}
