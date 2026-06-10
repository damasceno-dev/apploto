using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

/// <summary>
/// One would-be installment row inside the open-cheque aging impact preview. Mirrors the persisted
/// <see cref="ResponseOpenChequeAgingRowJson"/> the real report emits, except identity is positional
/// (<see cref="Index"/>) instead of a persisted <c>TransactionId</c> — nothing is created yet — and
/// the row's <see cref="Description"/> is surfaced so the caller can preview each cheque line.
/// </summary>
public class ResponseOpenChequeAgingPreviewRowJson
{
    public int Index { get; init; }
    public DateTime DueDate { get; init; }
    public decimal Value { get; init; }
    public int DaysOutstanding { get; init; }
    public AgingBucket Bucket { get; init; }
    public string? Description { get; init; }
}
