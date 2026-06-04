using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Transactions.EditPreview;

/// <summary>
/// Read-only overlay of a transaction edit. The constructor reads the no-tracking snapshot and
/// applies the payload as the hypothetical side; the snapshot itself is never mutated.
/// <para>
/// Single immutable fields (<see cref="TransactionId"/>, <see cref="BranchId"/>,
/// <see cref="AccountId"/>, <see cref="Date"/>, <see cref="AccountType"/>) are the cash-variance
/// close coordinates + identity. <c>AccountId</c>/<c>Date</c> stay single on purpose: if either
/// became editable the transaction would move between closes, which needs a list-shaped
/// cash-variance response — splitting them now would imply a capability that response does not have.
/// </para>
/// <para>
/// Every attribute whose divergence the response can already express is a <c>Current*</c>/
/// <c>Hypothetical*</c> pair. The update payload cannot change <c>Value</c> or <c>Direction</c>
/// today, so their <c>Hypothetical*</c> side equals the snapshot — but carrying the pair lets
/// <c>VarianceDelta</c> be computed, not assumed, and keeps the shape correct if those fields ever
/// become editable.
/// </para>
/// </summary>
public sealed class HypotheticalTransactionEdit(Transaction snapshot, RequestUpdateTransactionJson payload)
{
    public Guid TransactionId { get; } = snapshot.Id;
    public Guid BranchId { get; } = snapshot.BranchId;
    public Guid AccountId { get; } = snapshot.AccountId;
    public DateTime Date { get; } = snapshot.Date;
    public AccountType AccountType { get; } = snapshot.Account.Type;

    public decimal CurrentValue { get; } = snapshot.Value;
    public decimal HypotheticalValue { get; } = snapshot.Value;

    public Direction CurrentDirection { get; } = snapshot.Direction;
    public Direction HypotheticalDirection { get; } = snapshot.Direction;

    public Guid? CurrentClientId { get; } = snapshot.ClientId;
    public Guid? HypotheticalClientId { get; } = payload.ClientId;

    public DateTime CurrentDueDate { get; } = snapshot.DueDate;
    public DateTime HypotheticalDueDate { get; } = payload.DueDate;

    public DateTime? CurrentPaidAt { get; } = snapshot.PaidAt;
    public DateTime? HypotheticalPaidAt { get; } = payload.PaidAt;

    public TimeOnly? CurrentTransactionTime { get; } = snapshot.TransactionTime;
    public TimeOnly? HypotheticalTransactionTime { get; } = payload.TransactionTime;

    public string? CurrentDescription { get; } = snapshot.Description;
    public string? HypotheticalDescription { get; } = payload.Description;
}
