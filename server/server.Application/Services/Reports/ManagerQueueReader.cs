using server.Application.UseCases.Sessions;
using server.Application.Services.DailyCloses;
using server.Application.Services.Settings;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Reports;

/// <summary>
/// The Manager Work Queue computation for one branch-local date (M7.5 Phase 1, M7.7 Phase 7.5), extracted unchanged in
/// M11 Phase 7.4 so <c>GET /report/manager-queue</c> and the management overview read one rule. No authorization here.
/// </summary>
public class ManagerQueueReader(
    IDailyClosesRepository dailyClosesRepository,
    IDailyCloseItemsRepository dailyCloseItemsRepository,
    IAccountsRepository accountsRepository,
    ITransactionsRepository transactionsRepository,
    ICashVarianceProductResolver cashVarianceProductResolver,
    ILockDateReader lockDateReader,
    IOnboardingRepository onboardingRepository,
    ISessionHandoversRepository sessionHandoversRepository,
    ISessionOpeningDiscrepanciesRepository discrepanciesRepository,
    ICustodyMappingIssuesRepository custodyMappingIssuesRepository)
{
    public async Task<ResponseManagerQueueJson> Read(
        Guid branchId,
        DateTime date,
        DateTime branchLocalToday,
        CancellationToken ct = default)
    {
        var cashVarianceProductId = await cashVarianceProductResolver.GetIdAsync(branchId, ct);

        var closes = await dailyClosesRepository.ListManagerQueueClosesByBranchIdAndDateAsNoTracking(
            branchId, date, ct);

        var varianceRows = await dailyCloseItemsRepository.ListVarianceValuesByBranchIdAndProductIdAndDateRangeAsNoTracking(
            branchId, cashVarianceProductId, accountId: null, dateFrom: date, dateTo: date, ct);

        var expectedClosers = await accountsRepository.ListExpectedClosersByBranchIdAsNoTracking(
            branchId, ct);

        // Both aggregates start after LockDate but stop at different days: the close backlog ends
        // before the requested date (that day's closes are the rows above), while Draft transactions
        // are counted through branch-local today whatever date is being viewed.
        var lockDate = await lockDateReader.Read(branchId, ct);
        var backlog = await dailyClosesRepository.GetManagerQueueBacklogByBranchIdAndDateRangeAsNoTracking(
            branchId, afterDateExclusive: lockDate, beforeDateExclusive: date, ct);
        var unlockedDraftTransactionCount = await transactionsRepository.CountDraftByBranchIdAndDateRangeAsNoTracking(
            branchId, afterDateExclusive: lockDate, throughDateInclusive: branchLocalToday, ct);

        // Keyed by session: each session carries its own official variance; successive sessions of a shared cashbox
        // are listed side by side and never added together.
        var varianceBySession = varianceRows.ToDictionary(r => r.DailyCloseId, r => r.Value);

        // Draft closes never surface as review rows: they belong to the not-submitted queue below,
        // carrying their close id so the UI can deep-link into the open draft.
        var closeRows = closes
            .Where(c => c.Status != DailyCloseStatus.Draft)
            .Select(c => new ResponseManagerQueueCloseJson
            {
                DailyCloseId = c.DailyCloseId,
                AccountId = c.AccountId,
                AccountName = c.AccountName,
                RecordedByUserId = c.RecordedByUserId,
                RecordedByUserName = c.RecordedByUserName,
                RecordedByOperatorId = c.RecordedByOperatorId,
                RecordedByOperatorName = c.RecordedByOperatorName,
                SubmittedByUserId = c.SubmittedByUserId,
                SubmittedByUserName = c.SubmittedByUserName,
                SubmittedByOperatorId = c.SubmittedByOperatorId,
                SubmittedByOperatorName = c.SubmittedByOperatorName,
                Status = c.Status,
                SubmittedAt = c.SubmittedAt,
                ApprovedAt = c.ApprovedAt,
                VarianceValue = varianceBySession.TryGetValue(c.DailyCloseId, out var variance)
                    ? variance
                    : null
            })
            .ToList();

        var submittedAccountIds = closeRows.Select(c => c.AccountId).ToHashSet();
        // A cashbox may hold several Draft sessions on one day; the deep link names the latest.
        var draftClosesByAccountId = closes
            .Where(c => c.Status == DailyCloseStatus.Draft)
            .GroupBy(c => c.AccountId)
            .ToDictionary(group => group.Key, group => group.Last());

        // Expected-closer rule (M7.5 item 1.2): active Terminal accounts with at least one active
        // OperatorAccount link to an active Operator are expected to close. Bank/Tab accounts are not:
        // §6.5 daily closing covers terminal cash counts and §6.4 keeps fiado balances query-time.
        // A future branch-local date has no expected closers yet — nobody is missing a close for a
        // day that has not happened at the branch.
        List<ResponseManagerQueueNotSubmittedJson> notSubmitted = [];
        if (date <= branchLocalToday)
        {
            notSubmitted = expectedClosers
                .Where(expected => !submittedAccountIds.Contains(expected.AccountId))
                .Select(expected =>
                {
                    var draftClose = draftClosesByAccountId.GetValueOrDefault(expected.AccountId);
                    return new ResponseManagerQueueNotSubmittedJson
                    {
                        AccountId = expected.AccountId,
                        AccountName = expected.AccountName,
                        OperatorId = expected.OperatorId,
                        OperatorName = expected.OperatorName,
                        DailyCloseId = draftClose?.DailyCloseId,
                        Status = draftClose?.Status
                    };
                })
                .ToList();
        }

        var totalVariance = varianceRows.Sum(r => r.Value);
        var meanVariance = varianceRows.Count > 0 ? totalVariance / varianceRows.Count : 0m;

        return new ResponseManagerQueueJson
        {
            Date = date,
            TotalVariance = totalVariance,
            MeanVariance = meanVariance,
            PendingApprovalCount = closeRows.Count(c => c.Status == DailyCloseStatus.Submitted),
            Closes = closeRows,
            NotSubmitted = notSubmitted,
            Backlog = new ResponseManagerQueueBacklogJson
            {
                SubmittedCount = backlog.SubmittedCount,
                RejectedCount = backlog.RejectedCount,
                DraftCount = backlog.DraftCount,
                OldestPendingDate = backlog.OldestPendingDate
            },
            PendingOperatorLinkRequestCount = await onboardingRepository.PendingRequestCount(branchId, ct),
            Custody = await ReadCustody(branchId, branchLocalToday, ct),
            UnlockedDraftTransactionCount = unlockedDraftTransactionCount
        };
    }

    private async Task<ResponseManagerQueueCustodyJson> ReadCustody(Guid branchId, DateTime branchLocalToday, CancellationToken ct)
    {
        var openSessions = await dailyClosesRepository.ListActiveCustodyByBranchIdAsNoTracking(branchId, ct);
        var handovers = await sessionHandoversRepository.ListUnresolvedByBranchIdAsNoTracking(branchId, ct);
        var discrepancies = await discrepanciesRepository.ListAwaitingAttributionByBranchIdAsNoTracking(branchId, ct);
        var issues = await custodyMappingIssuesRepository.CountUnresolvedByBranchIdAsNoTracking(branchId, ct);

        return new ResponseManagerQueueCustodyJson
        {
            OpenSessions = openSessions
                .Select(session => new ResponseManagerQueueOpenSessionJson
                {
                    SessionId = session.Id,
                    AccountId = session.AccountId,
                    AccountName = session.Account.Name,
                    Date = session.Date,
                    Sequence = session.Sequence,
                    CustodianOperatorId = session.CustodianOperatorId,
                    CustodianOperatorName = session.CustodianOperator?.Name,
                    StartedAt = session.StartedAt,
                    Status = session.Status,
                    StartedBeforeToday = session.Date < branchLocalToday
                })
                .ToList(),
            PendingHandovers = handovers.Select(handover => handover.ToResponse()).ToList(),
            OpeningDiscrepanciesAwaitingAttribution = discrepancies
                .Select(discrepancy => new ResponseManagerQueueOpeningDiscrepancyJson
                {
                    OpeningDiscrepancyId = discrepancy.Id,
                    SessionId = discrepancy.SessionId,
                    SessionOpeningId = discrepancy.SessionOpeningId,
                    HandoverId = discrepancy.HandoverId,
                    PredecessorSessionId = discrepancy.PredecessorSessionId,
                    AccountId = discrepancy.Session.AccountId,
                    AccountName = discrepancy.Session.Account.Name,
                    Date = discrepancy.Session.Date,
                    CustodianOperatorName = discrepancy.Session.CustodianOperator?.Name,
                    CurrentRevision = discrepancy.CurrentRevision,
                    CurrentTotalDelta = discrepancy.CurrentTotalDelta,
                    Attribution = discrepancy.Attribution,
                    AttributedRevision = discrepancy.AttributedRevision,
                    Version = discrepancy.Version
                })
                .ToList(),
            UnresolvedMappingIssueCount = issues.Sum(row => row.Count)
        };
    }
}
