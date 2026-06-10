using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Transactions.InstallmentPreview;

/// <summary>
/// Read-only model of the N-row cheque plan a <c>POST /transaction/installment</c> would create,
/// assembled from the request, the resolved <see cref="TransactionCreateContext"/> (type/category/
/// account), and the already-generated <see cref="InstallmentPlanRow"/> list. Every row shares the
/// plan's branch, account, client, <see cref="Date"/>, <see cref="Status"/>, and
/// <see cref="Direction"/> — exactly what the write twin stamps onto each persisted row — while
/// <see cref="HypotheticalInstallmentRow.DueDate"/>/<see cref="HypotheticalInstallmentRow.Value"/>
/// vary per row.
/// <para>
/// <see cref="Direction"/> comes from the resolved type's <c>Category.DefaultDirection</c>, never the
/// payload (the §6.1 classification invariant, mirroring the write mapper). <see cref="Status"/> is
/// <c>Draft</c> when the payload sets <c>SaveAsDraft</c>, else <c>Active</c>.
/// <see cref="AccountName"/>/<see cref="AccountType"/> come from <c>context.Account</c>, and the
/// optional <see cref="ClientId"/>/<see cref="ClientName"/> from the request id plus
/// <c>context.Client</c> — both resolved once by <c>TransactionCreatePreamble</c>, so the projector
/// needs no second client load (and cannot fall back to an empty name after preamble validation).
/// Branch scope stays explicit on <see cref="BranchId"/>. The plan never persists.
/// </para>
/// </summary>
public sealed class HypotheticalTransactionInstallmentPlan
{
    public HypotheticalTransactionInstallmentPlan(RequestCreateTransactionInstallmentJson request, TransactionCreateContext context, IReadOnlyList<InstallmentPlanRow> rows)
    {
        BranchId = context.BranchUser.BranchId;
        AccountId = request.AccountId;
        AccountName = context.Account!.Name;
        AccountType = context.Account!.Type;
        ClientId = request.ClientId;
        ClientName = context.Client?.Name;
        Date = request.Date;
        Status = request.SaveAsDraft ? TransactionStatus.Draft : TransactionStatus.Active;
        Direction = context.TransactionType.Category.DefaultDirection;
        TotalValue = rows.Sum(row => row.Value);
        Rows = rows
            .Select(row => new HypotheticalInstallmentRow(row.DueDate, row.Value, row.Description))
            .ToList();
    }

    public Guid BranchId { get; }
    public Guid AccountId { get; }
    public string AccountName { get; }
    public AccountType AccountType { get; }

    public Guid? ClientId { get; }
    public string? ClientName { get; }

    public DateTime Date { get; }
    public TransactionStatus Status { get; }
    public Direction Direction { get; }

    public decimal TotalValue { get; }
    public IReadOnlyList<HypotheticalInstallmentRow> Rows { get; }
}

/// <summary>One would-be installment line: the per-row fields the write twin varies across the plan.</summary>
public sealed record HypotheticalInstallmentRow(DateTime DueDate, decimal Value, string Description);
