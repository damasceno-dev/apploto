using server.Application.Services.DailyCloses;
using server.Application.UseCases.Transactions.EditPreview;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Reports;

/// <summary>
/// Computes the read-only impact sections for a hypothetical transaction edit without
/// persisting anything. A concrete helper (no interface), wired and tested the same way as the
/// other helpers — <see cref="ReportAgingBucketizer"/>, InstallmentPlanBuilder,
/// TransactionCreatePreamble. Depends on <see cref="IDailyClosesRepository"/> (open-close lookup),
/// <see cref="IClientsRepository"/> (fiado client names), and <see cref="ICashVarianceCalculator"/> +
/// <see cref="ICashVarianceProductResolver"/> (live variance recompute), plus the static
/// <see cref="ReportAgingBucketizer"/>.
/// </summary>
public class TransactionEditImpactProjector(
    IDailyClosesRepository dailyClosesRepository,
    IClientsRepository clientsRepository,
    ICashVarianceCalculator cashVarianceCalculator,
    ICashVarianceProductResolver cashVarianceProductResolver)
{
    public async Task<ResponseTransactionEditImpactJson> Project(
        HypotheticalTransactionEdit edit,
        DateTime asOfDate,
        Guid branchId,
        CancellationToken ct = default)
    {
        return new ResponseTransactionEditImpactJson
        {
            ReceivableImpact = BuildReceivableImpact(edit, asOfDate),
            FiadoBalanceImpact = await BuildFiadoBalanceImpact(edit, branchId),
            CashVarianceImpact = await BuildCashVarianceImpact(edit, branchId, ct)
        };
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

        // §6.4 sign convention: Out raises the outstanding balance (+), In lowers it (−). Value and
        // Direction are immutable, so the current side is the transaction's actual signed value.
        var signedValue = edit.CurrentDirection == Direction.Out ? edit.CurrentValue : -edit.CurrentValue;

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

    private async Task<ResponseCashVarianceImpactJson> BuildCashVarianceImpact(
        HypotheticalTransactionEdit edit,
        Guid branchId,
        CancellationToken ct)
    {
        // §6.12: CashVariance = TotalClosing − TotalOpening − (In − Out). A change to the row's net
        // cash flow moves the variance by (beforeNetFlow − afterNetFlow). Computed in every branch —
        // it is 0 under today's edit contract (Value/Direction can't change) but is derived, never
        // hardcoded.
        var varianceDelta = BuildVarianceDelta(edit);

        var dailyClose = await dailyClosesRepository
            .GetByBranchIdAndAccountIdAndDateAsNoTracking(branchId, edit.AccountId, edit.Date, ct);

        if (dailyClose is null)
        {
            // No close opened for this (account, date) — nothing to reconcile.
            return new ResponseCashVarianceImpactJson { VarianceDelta = varianceDelta };
        }

        // CurrentVariance is the real §6.12 number, live-recomputed whenever a complete closing-count
        // set exists — i.e. any status except Draft (Submitted/Approved/Rejected all retain their
        // last-submitted counts). Withheld only for Draft, whose counts are still being entered, so a
        // number would be partial and misleading. The edit itself is gated by the lock date, not the
        // close status, so by here the edit is permitted regardless of status; DailyCloseStatus is
        // surfaced so the manager can judge the consequence of editing under a pending vs signed-off
        // vs repudiated close.
        decimal? currentVariance = null;
        if (dailyClose.Status is not DailyCloseStatus.Draft)
        {
            var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchId, ct);
            currentVariance = await cashVarianceCalculator.CalculateAsync(
                branchId, edit.AccountId, edit.Date, dailyClose.Id, cashVarianceProductId, ct);
        }

        return new ResponseCashVarianceImpactJson
        {
            AccountId = edit.AccountId,
            Date = edit.Date,
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
}
