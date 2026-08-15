using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.Services.DailyCloses;

public sealed class DailyCloseLedgerGuard(
    IDailyClosesRepository dailyClosesRepository,
    ITransactionsRepository transactionsRepository) : IDailyCloseLedgerGuard
{
    public async Task EnsureLedgerAcceptsNewRow(
        Guid branchId,
        Guid accountId,
        AccountType accountType,
        DateTime date,
        CancellationToken ct = default)
    {
        var close = await dailyClosesRepository.GetByBranchIdAndAccountIdAndDateAsNoTracking(
            branchId,
            accountId,
            date.Date,
            ct);

        if (close is null && accountType == AccountType.Terminal)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_REQUIRES_OPEN_DAILY_CLOSE);

        if (close is not null && close.Status != DailyCloseStatus.Draft)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
    }

    public async Task EnsureLedgerIsMutable(
        Guid branchId,
        Guid accountId,
        DateTime date,
        CancellationToken ct = default)
    {
        var close = await dailyClosesRepository.GetByBranchIdAndAccountIdAndDateAsNoTracking(
            branchId,
            accountId,
            date.Date,
            ct);

        if (close is not null && close.Status != DailyCloseStatus.Draft)
            throw new ConflictException(ResourcesErrorMessages.TRANSACTION_DAILY_CLOSE_LEDGER_FROZEN);
    }

    public async Task EnsureNoOutstandingDraftTransactions(
        Guid branchId,
        Guid accountId,
        DateTime date,
        CancellationToken ct = default)
    {
        if (await transactionsRepository.ExistsDraftByAccountAndDateAsNoTracking(
                branchId,
                accountId,
                date.Date,
                ct))
        {
            throw new ConflictException(ResourcesErrorMessages.DAILYCLOSE_OUTSTANDING_DRAFT_TRANSACTIONS);
        }
    }
}
