using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Transactions.CreatePreview;

/// <summary>
/// Read-only model of the row a <c>POST /transaction</c> would create, assembled from the request
/// plus the resolved <see cref="TransactionCreateContext"/> (type/category/account/client). Unlike
/// <c>HypotheticalTransactionEdit</c> there is no <c>Current*</c>/<c>Hypothetical*</c> pairing — a
/// create has no "before" state, so every field is single-valued.
/// <para>
/// <see cref="Direction"/> comes from the resolved type's <c>Category.DefaultDirection</c>, never
/// from the payload (the classification invariant from §6.1, mirroring the write mapper).
/// <see cref="Status"/> is <c>Draft</c> when the payload sets <c>SaveAsDraft</c>, else <c>Active</c>.
/// <see cref="DueDate"/> is the preamble's computed due date (the exact value the write twin
/// persists), falling back to <see cref="Date"/> only if the context left it unset.
/// </para>
/// <para>
/// <c>RequestCreateTransactionJson</c> carries no <c>PaidAt</c>, so a freshly created row is always
/// unpaid — there is no paid-state field to model here. The context is read, never persisted.
/// </para>
/// </summary>
public sealed class HypotheticalTransactionCreate(RequestCreateTransactionJson request, TransactionCreateContext context)
{
    public Guid AccountId { get; } = request.AccountId;
    public AccountType AccountType { get; } = context.Account!.Type;

    public decimal Value { get; } = request.Value;
    public Direction Direction { get; } = context.TransactionType.Category.DefaultDirection;

    public DateTime Date { get; } = request.Date;
    public TransactionStatus Status { get; } = request.SaveAsDraft ? TransactionStatus.Draft : TransactionStatus.Active;

    public Guid? ClientId { get; } = request.ClientId;
    public DateTime DueDate { get; } = context.DueDate ?? request.Date;
}
