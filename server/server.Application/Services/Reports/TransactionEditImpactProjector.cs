using server.Application.Services.DailyCloses;
using server.Application.UseCases.Transactions.CreatePreview;
using server.Application.UseCases.Transactions.EditPreview;
using server.Application.UseCases.Transactions.InstallmentPreview;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Reports;

/// <summary>
/// Computes the read-only impact sections for a hypothetical transaction change — an edit
/// (<see cref="Project"/>), a single create (<see cref="ProjectCreate"/>), and a cheque installment
/// plan (<see cref="ProjectInstallment"/>) — without persisting anything. A concrete helper (no
/// interface), wired and tested the same way as the other helpers — <see cref="ReportAgingBucketizer"/>,
/// InstallmentPlanBuilder, TransactionCreatePreamble. Depends on <see cref="IDailyClosesRepository"/>
/// (open-close lookup), <see cref="IClientsRepository"/> (fiado client names), and
/// <see cref="ICashVarianceCalculator"/> + <see cref="ICashVarianceProductResolver"/> (live variance
/// recompute), plus the static <see cref="ReportAgingBucketizer"/>. The edit and create projections
/// return the shared <see cref="ResponseTransactionImpactJson"/> envelope; the installment projection
/// returns <see cref="ResponseInstallmentPreviewImpactJson"/> (its receivable section is a group
/// rollup, not a single row). The legacy class name is retained because it stays the single
/// DI-registered Reports helper.
/// </summary>
public class TransactionEditImpactProjector(
    IDailyClosesRepository dailyClosesRepository,
    IClientsRepository clientsRepository,
    ICashVarianceCalculator cashVarianceCalculator,
    ICashVarianceProductResolver cashVarianceProductResolver)
{
    public async Task<ResponseTransactionImpactJson> Project(
        HypotheticalTransactionEdit edit,
        DateTime asOfDate,
        Guid branchId,
        CancellationToken ct = default)
    {
        return new ResponseTransactionImpactJson
        {
            ReceivableImpact = BuildReceivableImpact(edit, asOfDate),
            FiadoBalanceImpact = await BuildFiadoBalanceImpact(edit, branchId),
            CashVarianceImpact = await BuildCashVarianceImpact(edit, branchId, ct)
        };
    }

    /// <summary>
    /// Projects the impact of a would-be <c>POST /transaction</c> row. A <c>Draft</c> row is invisible
    /// to every Active-filtered surface (§6.4 fiado sums, open receivables, §6.12 cash variance), so
    /// all three sections short-circuit to empty/zero. An <c>Active</c> row appears in open
    /// receivables when it is a Tab row, pushes its signed value onto its Tab client, and moves the
    /// day's cash variance by the negative of its net flow (genuinely non-zero — the reason this phase
    /// exists, in contrast to the edit twin's structural zero).
    /// </summary>
    public async Task<ResponseTransactionImpactJson> ProjectCreate(
        HypotheticalTransactionCreate create,
        DateTime asOfDate,
        Guid branchId,
        CancellationToken ct = default)
    {
        if (create.Status == TransactionStatus.Draft)
            return new ResponseTransactionImpactJson();

        return new ResponseTransactionImpactJson
        {
            ReceivableImpact = BuildCreateReceivableImpact(create, asOfDate),
            FiadoBalanceImpact = await BuildCreateFiadoBalanceImpact(create, branchId),
            CashVarianceImpact = await BuildCreateCashVarianceImpact(create, branchId, ct)
        };
    }

    /// <summary>
    /// Projects the downstream impact of a would-be <c>POST /transaction/installment</c> cheque plan.
    /// A <c>Draft</c> plan is invisible to every Active-filtered surface (the open-cheque aging report,
    /// §6.4 fiado sums, §6.12 cash variance), so all three sections short-circuit to empty/zero. An
    /// <c>Active</c> plan is <b>aggregated</b> (not a per-row loop over <see cref="ProjectCreate"/>):
    /// one would-be open-cheque group, one optional fiado delta on the Tab client, and one cash-variance
    /// shift on the plan's <c>(account, date)</c>. The client name is resolved once and reused by both
    /// the group and the fiado delta.
    /// </summary>
    public async Task<ResponseInstallmentPreviewImpactJson> ProjectInstallment(
        HypotheticalTransactionInstallmentPlan plan,
        DateTime asOfDate,
        CancellationToken ct = default)
    {
        if (plan.Status == TransactionStatus.Draft)
            return new ResponseInstallmentPreviewImpactJson();

        // The plan carries the preamble-resolved client name (when present), so no second client load
        // is needed here — both the open-cheque group and the fiado delta read it directly.
        return new ResponseInstallmentPreviewImpactJson
        {
            OpenChequeAgingImpact = BuildInstallmentOpenChequeAgingImpact(plan, asOfDate),
            FiadoBalanceImpact = BuildInstallmentFiadoBalanceImpact(plan),
            CashVarianceImpact = await BuildInstallmentCashVarianceImpact(plan, ct)
        };
    }

    private static ResponseOpenChequeAgingImpactJson BuildInstallmentOpenChequeAgingImpact(
        HypotheticalTransactionInstallmentPlan plan,
        DateTime asOfDate)
    {
        // One would-be group. A fresh plan has no paid rows, so every generated row is open and
        // OpenRowCount == TotalRowCount == rows.Count. Per-row DaysOutstanding/Bucket follow the exact
        // rules GET /report/cheques/open-aging uses, so the preview is position-for-position
        // deterministic against the report once the same payload is committed.
        var rows = plan.Rows;
        var oldestOpenDueDate = rows.Min(row => row.DueDate);

        var previewRows = rows
            .Select((row, index) => new ResponseOpenChequeAgingPreviewRowJson
            {
                Index = index + 1,
                DueDate = row.DueDate,
                Value = row.Value,
                DaysOutstanding = Math.Max(0, (asOfDate.Date - row.DueDate.Date).Days),
                Bucket = ReportAgingBucketizer.BucketFor(row.DueDate, asOfDate),
                Description = row.Description
            })
            .ToList();

        return new ResponseOpenChequeAgingImpactJson
        {
            GroupAppearsInOpenCheques = true,
            OutstandingTotal = rows.Sum(row => row.Value),
            OldestOpenDueDate = oldestOpenDueDate,
            OldestOpenBucket = ReportAgingBucketizer.BucketFor(oldestOpenDueDate, asOfDate),
            OpenRowCount = rows.Count,
            TotalRowCount = rows.Count,
            ClientId = plan.ClientId,
            ClientName = plan.ClientName,
            AccountId = plan.AccountId,
            AccountName = plan.AccountName,
            // The report's group description is the origin row's; the origin is the first installment.
            Description = rows[0].Description,
            Rows = previewRows
        };
    }

    private static ResponseFiadoBalanceImpactJson BuildInstallmentFiadoBalanceImpact(
        HypotheticalTransactionInstallmentPlan plan)
    {
        // Fiado balances live on Tab accounts with a client (§6.4). Every row shares the plan's
        // direction, so the whole plan lands as one aggregated signed delta on the client. This closes
        // the parity hole for cheque types that also require a Tab account + client.
        if (plan.AccountType != AccountType.Tab || plan.ClientId is not { } clientId)
            return new ResponseFiadoBalanceImpactJson();

        var signedTotal = plan.Rows.Sum(row => OutstandingDelta(plan.Direction, row.Value));

        return new ResponseFiadoBalanceImpactJson
        {
            Deltas =
            [
                new ResponseClientBalanceDeltaJson
                {
                    ClientId = clientId,
                    ClientName = plan.ClientName ?? string.Empty,
                    OutstandingDelta = signedTotal
                }
            ]
        };
    }

    private Task<ResponseCashVarianceImpactJson> BuildInstallmentCashVarianceImpact(
        HypotheticalTransactionInstallmentPlan plan,
        CancellationToken ct)
    {
        // The whole plan adds its summed net flow to (In − Out) on the shared (account, Date), so the
        // variance moves by the negative of that sum. ProjectedVariance is "the variance if current
        // close counts remain unchanged after these cheque rows are recorded" — later count
        // adjustments can offset it; the preview reports the immediate ledger-side consequence.
        var varianceDelta = -plan.Rows.Sum(row => NetFlow(plan.Direction, row.Value));
        return BuildCashVarianceSection(plan.BranchId, plan.AccountId, plan.Date, varianceDelta, ct);
    }

    private static ResponseReceivableImpactJson BuildReceivableImpact(HypotheticalTransactionEdit edit, DateTime asOfDate)
    {
        var currentlyOpen = edit.CurrentPaidAt is null;
        var hypotheticallyOpen = edit.HypotheticalPaidAt is null;

        return currentlyOpen switch
        {
            // Unpaid → unpaid: a pure DueDate shift moves the row between buckets.
            true when hypotheticallyOpen => new ResponseReceivableImpactJson { BucketBefore = ReportAgingBucketizer.BucketFor(edit.CurrentDueDate, asOfDate), BucketAfter = ReportAgingBucketizer.BucketFor(edit.HypotheticalDueDate, asOfDate) },
            // Unpaid → paid: the row leaves the open-receivables set.
            true when hypotheticallyOpen is false => new ResponseReceivableImpactJson { BucketBefore = ReportAgingBucketizer.BucketFor(edit.CurrentDueDate, asOfDate), BucketAfter = null, RowDisappearsFromOpenReceivables = true },
            // Paid → unpaid: the row re-enters the open-receivables set.
            false when hypotheticallyOpen => new ResponseReceivableImpactJson { BucketBefore = null, BucketAfter = ReportAgingBucketizer.BucketFor(edit.HypotheticalDueDate, asOfDate), RowAppearsInOpenReceivables = true },
            _ => new ResponseReceivableImpactJson()
        };

        // Paid → paid: no open-receivables impact.
    }

    private async Task<ResponseFiadoBalanceImpactJson> BuildFiadoBalanceImpact(HypotheticalTransactionEdit edit, Guid branchId)
    {
        // Fiado balances live on Tab accounts only; reassigning the client moves the signed value
        // off the old client and onto the new one. No other editable field touches a client balance.
        if (edit.AccountType != AccountType.Tab || edit.CurrentClientId == edit.HypotheticalClientId)
            return new ResponseFiadoBalanceImpactJson();

        // Value and Direction are immutable, so the current side is the transaction's actual signed value.
        var signedValue = OutstandingDelta(edit.CurrentDirection, edit.CurrentValue);

        var deltas = new List<ResponseClientBalanceDeltaJson>();

        if (edit.CurrentClientId is { } currentClientId)
            deltas.Add(await BuildDelta(currentClientId, -signedValue, branchId));

        if (edit.HypotheticalClientId is { } hypotheticalClientId)
            deltas.Add(await BuildDelta(hypotheticalClientId, signedValue, branchId));

        return new ResponseFiadoBalanceImpactJson { Deltas = deltas };
    }

    private async Task<ResponseClientBalanceDeltaJson> BuildDelta(Guid clientId, decimal outstandingDelta, Guid branchId)
    {
        // IClientsRepository.GetActiveByIdAndBranchIdAsNoTracking has no CancellationToken overload
        // (pre-M7 interface, many callers); not expanded in this slice.
        var client = await clientsRepository.GetActiveByIdAndBranchIdAsNoTracking(clientId, branchId);
        return new ResponseClientBalanceDeltaJson
        {
            ClientId = clientId,
            ClientName = client?.Name ?? string.Empty,
            OutstandingDelta = outstandingDelta
        };
    }

    private Task<ResponseCashVarianceImpactJson> BuildCashVarianceImpact(
        HypotheticalTransactionEdit edit,
        Guid branchId,
        CancellationToken ct)
    {
        // A change to the row's net cash flow moves the variance by (beforeNetFlow − afterNetFlow).
        // Computed in every branch — it is 0 under today's edit contract (Value/Direction can't
        // change) but is derived, never hardcoded. The edit itself is gated by the lock date, not the
        // close status, so by here the edit is permitted regardless of status; DailyCloseStatus lets
        // the manager judge the consequence of editing under a pending vs signed-off vs repudiated
        // close.
        return BuildCashVarianceSection(branchId, edit.AccountId, edit.Date, BuildVarianceDelta(edit), ct);
    }

    private static ResponseReceivableImpactJson BuildCreateReceivableImpact(HypotheticalTransactionCreate create, DateTime asOfDate)
    {
        // Open receivables are Tab-account, unpaid rows (§6.14 fiado aging). A new row carries no
        // PaidAt, so a Tab create always appears; a non-Tab row never enters the open-receivables set.
        if (create.AccountType != AccountType.Tab)
            return new ResponseReceivableImpactJson();

        return new ResponseReceivableImpactJson
        {
            BucketBefore = null,
            BucketAfter = ReportAgingBucketizer.BucketFor(create.DueDate, asOfDate),
            RowAppearsInOpenReceivables = true
        };
    }

    private async Task<ResponseFiadoBalanceImpactJson> BuildCreateFiadoBalanceImpact(HypotheticalTransactionCreate create, Guid branchId)
    {
        // Fiado balances live on Tab accounts with a client. The new row pushes its signed value onto
        // its client.
        if (create.AccountType != AccountType.Tab || create.ClientId is not { } clientId)
            return new ResponseFiadoBalanceImpactJson();

        var signedValue = OutstandingDelta(create.Direction, create.Value);

        return new ResponseFiadoBalanceImpactJson
        {
            Deltas = [await BuildDelta(clientId, signedValue, branchId)]
        };
    }

    private Task<ResponseCashVarianceImpactJson> BuildCreateCashVarianceImpact(
        HypotheticalTransactionCreate create,
        Guid branchId,
        CancellationToken ct)
    {
        // The new row adds its net flow to (In − Out), so the variance moves by the negative of that
        // net flow — genuinely non-zero, unlike the edit twin whose editable fields never touch
        // §6.12's inputs.
        var varianceDelta = -NetFlow(create.Direction, create.Value);
        return BuildCashVarianceSection(branchId, create.AccountId, create.Date, varianceDelta, ct);
    }

    /// <summary>
    /// Shared §6.12 pipeline for all three projections: CashVariance = TotalClosing − TotalOpening −
    /// (In − Out). The caller computes how its hypothetical change moves (In − Out) and passes the
    /// resulting <paramref name="varianceDelta"/>; everything from the close lookup onward is
    /// scenario-independent. Exactly one close is modeled: the scenario's (account, date) is fixed.
    /// </summary>
    private async Task<ResponseCashVarianceImpactJson> BuildCashVarianceSection(
        Guid branchId,
        Guid accountId,
        DateTime date,
        decimal varianceDelta,
        CancellationToken ct)
    {
        var dailyClose = await dailyClosesRepository
            .GetByBranchIdAndAccountIdAndDateAsNoTracking(branchId, accountId, date, ct);

        if (dailyClose is null)
        {
            // No close opened for this (account, date) — nothing to reconcile against.
            return new ResponseCashVarianceImpactJson { VarianceDelta = varianceDelta };
        }

        // CurrentVariance is the real §6.12 number, live-recomputed whenever a complete closing-count
        // set exists — any status except Draft (Submitted/Approved/Rejected all retain their
        // last-submitted counts). Withheld only for Draft, whose counts are still being entered, so a
        // number would be partial and misleading. DailyCloseStatus is surfaced so the caller can
        // judge whether the change lands under a pending, signed-off, or repudiated close.
        decimal? currentVariance = null;
        if (dailyClose.Status is not DailyCloseStatus.Draft)
        {
            var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchId, ct);
            currentVariance = await cashVarianceCalculator.CalculateAsync(
                branchId, accountId, date, dailyClose.Id, cashVarianceProductId, ct);
        }

        return new ResponseCashVarianceImpactJson
        {
            AccountId = accountId,
            Date = date,
            DailyCloseStatus = dailyClose.Status,
            CurrentVariance = currentVariance,
            ProjectedVariance = currentVariance is { } current ? current + varianceDelta : null,
            VarianceDelta = varianceDelta
        };
    }

    private static decimal BuildVarianceDelta(HypotheticalTransactionEdit edit)
    {
        var beforeNetFlow = NetFlow(edit.CurrentDirection, edit.CurrentValue);
        var afterNetFlow = NetFlow(edit.HypotheticalDirection, edit.HypotheticalValue);
        return beforeNetFlow - afterNetFlow;
    }

    private static decimal NetFlow(Direction direction, decimal value)
    {
        return direction == Direction.In ? value : -value;
    }

    /// <summary>
    /// §6.4 sign convention for fiado balances: Out raises the client's outstanding balance (+), In
    /// lowers it (−). Kept separate from <see cref="NetFlow"/> even though the two are mirror images
    /// today — §6.4 (outstanding balance) and §6.12 (cash flow) are independent conventions.
    /// </summary>
    private static decimal OutstandingDelta(Direction direction, decimal value)
    {
        return direction == Direction.Out ? value : -value;
    }
}
