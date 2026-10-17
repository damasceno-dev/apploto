using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Models;
using server.Domain.Models.Projections;

namespace server.Domain.Interfaces;

public interface ITransactionsRepository
{
    Task Add(Transaction transaction, CancellationToken ct = default);
    Task AddRange(IEnumerable<Transaction> transactions, CancellationToken ct = default);
    Task<Transaction?> GetByIdAndBranchId(Guid id, Guid branchId, CancellationToken ct = default);
    /// <summary>The generated Terminal counterpart of a fiado source row (M7.7 Phase 5.6), tracked.</summary>
    Task<Transaction?> GetGeneratedBySourceIdAndBranchId(Guid sourceId, Guid branchId, CancellationToken ct = default);

    /// <summary>The generated Terminal counterpart of a fiado source row, read-only.</summary>
    Task<Transaction?> GetGeneratedBySourceIdAndBranchIdAsNoTracking(Guid sourceId, Guid branchId, CancellationToken ct = default);

    Task<Transaction?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task<Transaction?> GetByIdAndBranchIdAsNoTrackingWithTransactionType(Guid id, Guid branchId);
    Task<IReadOnlyList<Transaction>> ListByBranchIdAsNoTracking(Guid branchId, TransactionListFilter filter);
    Task<int> CountByBranchIdAsNoTracking(Guid branchId, TransactionListFilter filter);
    Task<IReadOnlyList<Transaction>> ListByOriginTransactionIdAndBranchIdAsNoTracking(Guid originId, Guid branchId);
    /// <summary>
    /// Tracked: every installment of one plan in the branch (any status, non-soft-deleted), ordered
    /// <c>DueDate, Id</c>. The authoritative sibling set for plan commands.
    /// </summary>
    Task<IReadOnlyList<Transaction>> ListPlanInstallmentsByOriginTransactionIdAndBranchId(
        Guid originTransactionId, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> ListPlanInstallmentsByOriginTransactionIdAndBranchIdAsNoTracking(
        Guid originTransactionId, Guid branchId, CancellationToken ct = default);
    Task<decimal> SumActiveValueByAccountAndDateAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime date,
        Direction? direction = null,
        CancellationToken ct = default);
    Task<bool> ExistsDraftByAccountAndDateAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime date,
        CancellationToken ct = default);
    Task<DateTime?> GetEarliestUncountedActivityDateByAccountAsNoTracking(
        Guid branchId,
        Guid accountId,
        DateTime afterDateExclusive,
        DateTime beforeDateExclusive,
        CancellationToken ct = default);
    /// <summary>
    /// Earliest business date carrying a non-soft-deleted transaction in the branch, across every status.
    /// Used to keep a fresh branch's month-lock floor from skipping backdated/imported history.
    /// </summary>
    Task<DateTime?> GetEarliestDateByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);

    Task<IReadOnlyList<Transaction>> ListByBranchIdAndAccountIdAndDateRangeAsNoTracking(Guid branchId, DailyLedgerListFilter filter);
    Task<int> CountByBranchIdAndAccountIdAndDateRangeAsNoTracking(Guid branchId, DailyLedgerListFilter filter);
    Task<decimal> SumActiveByAccountAndDateBeforeAsNoTracking(Guid branchId, Guid accountId, DateTime dateExclusive);
    Task<(decimal totalIn, decimal totalOut)> SumActiveByAccountAndDateRangeAsNoTracking(Guid branchId, Guid accountId, DateTime dateFrom, DateTime dateTo);

    /// <summary>
    /// Branch-wide fiado balances per client (§6.4): signed <c>Out − In</c> over active Tab rows with a client dated
    /// on or before <paramref name="asOfDate"/>, across every Tab; zero balances are omitted. Shares one SQL netting
    /// projection with <see cref="ListFiadoOpenSalesByBranchIdAsNoTracking"/> and
    /// <see cref="GetFiadoClientPositionAsNoTracking"/>.
    /// </summary>
    Task<IReadOnlyList<FiadoClientBalanceRow>> ListFiadoBalancesByBranchIdAsNoTracking(
        Guid branchId, Guid? clientId, DateTime asOfDate, CancellationToken ct = default);

    /// <summary>
    /// Fiado sales still holding a positive unpaid remainder after branch/client-wide FIFO netting: every In of the
    /// client (any Tab) is applied to its Out rows ordered <c>DueDate, Date, Id</c>. Repayment In rows and credit
    /// never appear. <paramref name="accountId"/> filters the netted result, never the netting input. Ordered
    /// <c>DueDate, Date, Id</c>.
    /// </summary>
    Task<IReadOnlyList<FiadoOpenSaleRow>> ListFiadoOpenSalesByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, DateTime asOfDate, int page, int pageSize, CancellationToken ct = default);
    Task<int> CountFiadoOpenSalesByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, DateTime asOfDate, CancellationToken ct = default);

    /// <summary>
    /// Whole-set FIFO aging totals from the same projection and filters as <see cref="ListFiadoOpenSalesByBranchIdAsNoTracking"/>
    /// (M11 Phase 3.2), grouped by due date, plus the earliest sale date still holding a remainder.
    /// </summary>
    Task<FiadoAgingTotals> GetFiadoAgingTotalsByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, DateTime asOfDate, CancellationToken ct = default);

    /// <summary>
    /// A client's fiado statement page over the same projection: movements dated in
    /// <c>[dateFrom ?? first movement, dateTo]</c>, ordered <c>Date DESC, CreatedAt DESC, Id DESC</c>, each with its
    /// running balance over the client's full chronological history (<c>Date, CreatedAt, Id</c>).
    /// </summary>
    Task<IReadOnlyList<FiadoStatementRow>> ListFiadoStatementByClientIdAsNoTracking(
        Guid branchId, Guid clientId, DateTime? dateFrom, DateTime dateTo, int page, int pageSize, CancellationToken ct = default);

    /// <summary>The statement window's opening/closing balances and movement count, across every page.</summary>
    Task<FiadoStatementWindow> GetFiadoStatementWindowByClientIdAsNoTracking(
        Guid branchId, Guid clientId, DateTime? dateFrom, DateTime dateTo, CancellationToken ct = default);

    /// <summary>One client's branch-wide fiado position from the same netting projection.</summary>
    Task<FiadoClientPosition> GetFiadoClientPositionAsNoTracking(
        Guid branchId, Guid clientId, DateTime asOfDate, CancellationToken ct = default);

    Task<OperatorTransactionSummaryProjection> SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
        Guid branchId, Guid operatorId, DateTime dateFrom, DateTime dateTo, IReadOnlyList<Guid>? allowedAccountIds = null);

    Task<IReadOnlyList<OpenChequeGroupRow>> ListOpenChequeGroupsByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, int page, int pageSize);
    Task<int> CountOpenChequeGroupsByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId);

    /// <summary>
    /// Whole-filter open-cheque totals (M11 Phase 2.1): every unpaid row of every open plan matching the filters,
    /// grouped by due date in one query, independent of paging.
    /// </summary>
    Task<IReadOnlyList<AgingDueDateTotalRow>> ListOpenChequeTotalsByDueDateAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> ListActiveByOriginTransactionIdAndBranchIdAsNoTracking(
        Guid originId, Guid branchId);
    /// <summary>
    /// Counts active, non-soft-deleted transactions for the branch/month grouped by (Date, Status), spanning
    /// all Active/Draft/Canceled. A dedicated count aggregate — NOT the paginated list — so it counts in
    /// the database without materializing every row and cannot silently drop rows past a page boundary.
    /// </summary>
    Task<IReadOnlyList<MonthlyTransactionCountRow>> CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
        Guid branchId, int year, int month, CancellationToken ct = default);
    /// <summary>
    /// Counts non-soft-deleted Draft transactions for the branch with
    /// <paramref name="afterDateExclusive"/> &lt; Date ≤ <paramref name="throughDateInclusive"/>.
    /// </summary>
    Task<int> CountDraftByBranchIdAndDateRangeAsNoTracking(
        Guid branchId, DateTime afterDateExclusive, DateTime throughDateInclusive, CancellationToken ct = default);
    /// <summary>
    /// Distinct dates and Terminal accounts carrying direct active cash-ledger activity.
    /// Tab activity is intentionally excluded even when the Tab is paired to a Terminal.
    /// </summary>
    Task<IReadOnlyList<TerminalActivityPairRow>> ListActiveTerminalActivityPairsByBranchIdAndYearMonthAsNoTracking(
        Guid branchId, int year, int month, CancellationToken ct = default);
}
