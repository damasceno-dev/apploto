using server.Domain.Entities.Enums;

namespace server.Application.Services.DailyCloses;

public interface IDailyCloseLedgerGuard
{
    Task EnsureLedgerAcceptsNewRow(
        Guid branchId,
        Guid accountId,
        AccountType accountType,
        DateTime date,
        CancellationToken ct = default);
    Task EnsureLedgerIsMutable(Guid branchId, Guid accountId, DateTime date, CancellationToken ct = default);
    Task EnsureNoOutstandingDraftTransactions(Guid branchId, Guid accountId, DateTime date, CancellationToken ct = default);
}
