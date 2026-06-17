using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Models;
using server.Domain.Models.Projections;

namespace server.Domain.Interfaces;

public interface ITransactionsRepository
{
    Task Add(Transaction transaction);
    Task AddRange(IEnumerable<Transaction> transactions);
    Task<Transaction?> GetByIdAndBranchId(Guid id, Guid branchId);
    Task<Transaction?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId);
    Task<Transaction?> GetByIdAndBranchIdAsNoTrackingWithTransactionType(Guid id, Guid branchId);
    Task<IReadOnlyList<Transaction>> ListByBranchIdAsNoTracking(Guid branchId, TransactionListFilter filter);
    Task<int> CountByBranchIdAsNoTracking(Guid branchId, TransactionListFilter filter);
    Task<IReadOnlyList<Transaction>> ListByOriginTransactionIdAndBranchIdAsNoTracking(Guid originId, Guid branchId);
    Task<decimal> SumActiveValueByAccountAndDateAsNoTracking(Guid branchId, Guid accountId, DateTime date, Direction? direction = null);

    Task<IReadOnlyList<Transaction>> ListByBranchIdAndAccountIdAndDateRangeAsNoTracking(Guid branchId, DailyLedgerListFilter filter);
    Task<int> CountByBranchIdAndAccountIdAndDateRangeAsNoTracking(Guid branchId, DailyLedgerListFilter filter);
    Task<decimal> SumActiveByAccountAndDateBeforeAsNoTracking(Guid branchId, Guid accountId, DateTime dateExclusive);
    Task<(decimal totalIn, decimal totalOut)> SumActiveByAccountAndDateRangeAsNoTracking(Guid branchId, Guid accountId, DateTime dateFrom, DateTime dateTo);

    Task<IReadOnlyList<FiadoClientBalanceRow>> ListFiadoBalancesByBranchIdAsNoTracking(Guid branchId, Guid? clientId, DateTime asOfDate);

    Task<IReadOnlyList<TransactionOpenReceivableRow>> ListOpenReceivablesByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, DateTime asOfDate, int page, int pageSize, AccountType? accountType = null);
    Task<int> CountOpenReceivablesByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, DateTime asOfDate, AccountType? accountType = null);

    Task<OperatorTransactionSummaryProjection> SumByBranchIdAndOperatorIdAndDateRangeAsNoTracking(
        Guid branchId, Guid operatorId, DateTime dateFrom, DateTime dateTo, IReadOnlyList<Guid>? allowedAccountIds = null);

    Task<IReadOnlyList<OpenChequeGroupRow>> ListOpenChequeGroupsByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId, int page, int pageSize);
    Task<int> CountOpenChequeGroupsByBranchIdAsNoTracking(
        Guid branchId, Guid? accountId, Guid? clientId);
    Task<IReadOnlyList<Transaction>> ListActiveByOriginTransactionIdAndBranchIdAsNoTracking(
        Guid originId, Guid branchId);
    /// <summary>
    /// Counts active, non-soft-deleted transactions for the branch/month grouped by (Date, Status), spanning
    /// all Active/Draft/Canceled. A dedicated count aggregate — NOT the paginated list — so it counts in
    /// the database without materializing every row and cannot silently drop rows past a page boundary.
    /// </summary>
    Task<IReadOnlyList<MonthlyTransactionCountRow>> CountByBranchIdAndYearMonthGroupedByDateAndStatusAsNoTracking(
        Guid branchId, int year, int month);
}
