using Microsoft.EntityFrameworkCore;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Infrastructure.Repositories;

internal class TerminalsRepository(ServerDbContext dbContext) : ITerminalsRepository
{
    public async Task Add(Terminal terminal, CancellationToken ct = default)
    {
        await dbContext.Terminals.AddAsync(terminal, ct);
    }

    public Task<Terminal?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default)
    {
        return dbContext.Terminals
            .AsNoTracking()
            .FirstOrDefaultAsync(terminal => terminal.Id == id && terminal.BranchId == branchId && terminal.Active, ct);
    }

    public async Task<IReadOnlyList<Terminal>> ListActiveByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default)
    {
        return await dbContext.Terminals
            .AsNoTracking()
            .Where(terminal => terminal.BranchId == branchId && terminal.Active)
            .OrderBy(terminal => terminal.Name)
            .ThenBy(terminal => terminal.Id)
            .ToListAsync(ct);
    }
}

internal class SessionOpeningsRepository(ServerDbContext dbContext) : ISessionOpeningsRepository
{
    public async Task Add(SessionOpening opening, CancellationToken ct = default)
    {
        await dbContext.SessionOpenings.AddAsync(opening, ct);
    }
}

internal class SessionHandoversRepository(ServerDbContext dbContext) : ISessionHandoversRepository
{
    private static readonly SessionHandoverState[] UnresolvedStates =
        [SessionHandoverState.Offered, SessionHandoverState.Disputed];

    public async Task Add(SessionHandover handover, CancellationToken ct = default)
    {
        await dbContext.SessionHandovers.AddAsync(handover, ct);
    }

    public Task<SessionHandover?> GetByIdAndBranchId(Guid id, Guid branchId, CancellationToken ct = default)
    {
        return dbContext.SessionHandovers
            .Include(handover => handover.Items)
            .Include(handover => handover.IntendedIncomingOperator)
            .FirstOrDefaultAsync(handover => handover.Id == id && handover.BranchId == branchId && handover.Active, ct);
    }

    public Task<SessionHandover?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default)
    {
        return dbContext.SessionHandovers
            .AsNoTracking()
            .Include(handover => handover.Items)
            .Include(handover => handover.IntendedIncomingOperator)
            .FirstOrDefaultAsync(handover => handover.Id == id && handover.BranchId == branchId && handover.Active, ct);
    }

    public Task<SessionHandover?> GetUnresolvedByOutgoingSessionId(
        Guid branchId,
        Guid outgoingSessionId,
        CancellationToken ct = default)
    {
        return dbContext.SessionHandovers
            .Include(handover => handover.Items)
            .FirstOrDefaultAsync(handover =>
                handover.BranchId == branchId &&
                handover.OutgoingSessionId == outgoingSessionId &&
                handover.Active &&
                UnresolvedStates.Contains(handover.State),
                ct);
    }

    public Task<bool> ExistsUnresolvedByAccountIdAsNoTracking(Guid branchId, Guid accountId, CancellationToken ct = default)
    {
        return dbContext.SessionHandovers
            .AsNoTracking()
            .AnyAsync(handover =>
                handover.BranchId == branchId &&
                handover.AccountId == accountId &&
                handover.Active &&
                UnresolvedStates.Contains(handover.State),
                ct);
    }

    public async Task<IReadOnlyList<SessionHandover>> ListUnresolvedByBranchIdAsNoTracking(
        Guid branchId,
        CancellationToken ct = default)
    {
        return await dbContext.SessionHandovers
            .AsNoTracking()
            .Include(handover => handover.IntendedIncomingOperator)
            .Where(handover =>
                handover.BranchId == branchId &&
                handover.Active &&
                UnresolvedStates.Contains(handover.State))
            .OrderBy(handover => handover.OfferedAt)
            .ThenBy(handover => handover.Id)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SessionHandover>> ListBySessionIdAsNoTracking(
        Guid branchId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        return await dbContext.SessionHandovers
            .AsNoTracking()
            .Include(handover => handover.Items)
            .Include(handover => handover.IntendedIncomingOperator)
            .Where(handover =>
                handover.BranchId == branchId &&
                handover.Active &&
                (handover.OutgoingSessionId == sessionId || handover.IncomingSessionId == sessionId))
            .OrderBy(handover => handover.OfferedAt)
            .ThenBy(handover => handover.Id)
            .ToListAsync(ct);
    }
}

internal class SessionOpeningDiscrepanciesRepository(ServerDbContext dbContext) : ISessionOpeningDiscrepanciesRepository
{
    public async Task Add(SessionOpeningDiscrepancy discrepancy, CancellationToken ct = default)
    {
        await dbContext.SessionOpeningDiscrepancies.AddAsync(discrepancy, ct);
    }

    public async Task AddAttribution(OpeningDiscrepancyAttributionEvent attribution, CancellationToken ct = default)
    {
        await dbContext.OpeningDiscrepancyAttributionEvents.AddAsync(attribution, ct);
    }

    public async Task AddRevision(SessionOpeningDiscrepancyRevision revision, CancellationToken ct = default)
    {
        // Added explicitly: a client-keyed child discovered only through a tracked parent's collection would be
        // treated as an existing row and updated.
        await dbContext.SessionOpeningDiscrepancyRevisions.AddAsync(revision, ct);
    }

    public Task<SessionOpeningDiscrepancy?> GetByIdAndBranchId(Guid id, Guid branchId, CancellationToken ct = default)
    {
        return WithRevisions(dbContext.SessionOpeningDiscrepancies)
            .FirstOrDefaultAsync(discrepancy => discrepancy.Id == id && discrepancy.BranchId == branchId, ct);
    }

    public Task<SessionOpeningDiscrepancy?> GetBySessionId(Guid branchId, Guid sessionId, CancellationToken ct = default)
    {
        return WithRevisions(dbContext.SessionOpeningDiscrepancies)
            .FirstOrDefaultAsync(discrepancy => discrepancy.BranchId == branchId && discrepancy.SessionId == sessionId, ct);
    }

    public Task<SessionOpeningDiscrepancy?> GetBySessionIdAsNoTracking(
        Guid branchId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        return WithRevisions(dbContext.SessionOpeningDiscrepancies)
            .AsNoTracking()
            .FirstOrDefaultAsync(discrepancy => discrepancy.BranchId == branchId && discrepancy.SessionId == sessionId, ct);
    }

    public Task<OpeningDiscrepancyAttributionEvent?> GetLatestAttributionAsNoTracking(
        Guid discrepancyId,
        CancellationToken ct = default)
    {
        return dbContext.OpeningDiscrepancyAttributionEvents
            .AsNoTracking()
            .Where(attribution => attribution.DiscrepancyId == discrepancyId)
            .OrderByDescending(attribution => attribution.CreatedAt)
            .ThenByDescending(attribution => attribution.Id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<SessionOpeningDiscrepancy>> ListAwaitingAttributionByBranchIdAsNoTracking(
        Guid branchId,
        CancellationToken ct = default)
    {
        return await dbContext.SessionOpeningDiscrepancies
            .AsNoTracking()
            .Include(discrepancy => discrepancy.Session)
            .ThenInclude(session => session.Account)
            .Include(discrepancy => discrepancy.Session)
            .ThenInclude(session => session.CustodianOperator)
            .Where(discrepancy =>
                discrepancy.BranchId == branchId &&
                discrepancy.Active &&
                discrepancy.CurrentTotalDelta != 0 &&
                (discrepancy.Attribution == OpeningDiscrepancyAttribution.Unattributed ||
                 discrepancy.AttributedRevision != discrepancy.CurrentRevision))
            .OrderBy(discrepancy => discrepancy.Session.Date)
            .ThenBy(discrepancy => discrepancy.Id)
            .ToListAsync(ct);
    }

    private static IQueryable<SessionOpeningDiscrepancy> WithRevisions(IQueryable<SessionOpeningDiscrepancy> source)
    {
        return source
            .Include(discrepancy => discrepancy.Revisions)
            .ThenInclude(revision => revision.Items);
    }
}

internal class SessionEventsRepository(ServerDbContext dbContext) : ISessionEventsRepository
{
    public async Task Add(SessionEvent sessionEvent, CancellationToken ct = default)
    {
        await dbContext.SessionEvents.AddAsync(sessionEvent, ct);
    }

    public async Task<IReadOnlyList<SessionEvent>> ListBySessionIdAsNoTracking(
        Guid branchId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        return await dbContext.SessionEvents
            .AsNoTracking()
            .Where(sessionEvent => sessionEvent.BranchId == branchId && sessionEvent.SessionId == sessionId)
            .OrderBy(sessionEvent => sessionEvent.CreatedAt)
            .ThenBy(sessionEvent => sessionEvent.Kind)
            .ThenBy(sessionEvent => sessionEvent.Id)
            .ToListAsync(ct);
    }
}

internal class SessionCountAttemptsRepository(ServerDbContext dbContext) : ISessionCountAttemptsRepository
{
    public async Task Add(SessionCountAttempt attempt, CancellationToken ct = default)
    {
        await dbContext.SessionCountAttempts.AddAsync(attempt, ct);
    }

    public async Task<SessionCountAttempt?> GetBySessionIdAndTokenAsNoTracking(
        Guid branchId,
        Guid sessionId,
        Guid token,
        CancellationToken ct = default)
    {
        return await WithItems()
            .FirstOrDefaultAsync(attempt =>
                attempt.BranchId == branchId && attempt.SessionId == sessionId && attempt.ConfirmationToken == token,
                ct);
    }

    public async Task<SessionCountAttempt?> GetBySessionIdAndNumberAsNoTracking(
        Guid branchId,
        Guid sessionId,
        int attemptNumber,
        CancellationToken ct = default)
    {
        return await WithItems()
            .FirstOrDefaultAsync(attempt =>
                attempt.BranchId == branchId && attempt.SessionId == sessionId && attempt.AttemptNumber == attemptNumber,
                ct);
    }

    public async Task<SessionCountAttempt?> GetByIdAndSessionIdAsNoTracking(
        Guid branchId,
        Guid sessionId,
        Guid attemptId,
        CancellationToken ct = default)
    {
        return await WithItems()
            .FirstOrDefaultAsync(attempt =>
                attempt.BranchId == branchId && attempt.SessionId == sessionId && attempt.Id == attemptId,
                ct);
    }

    public async Task<IReadOnlyList<SessionCountAttempt>> ListBySessionIdAsNoTracking(
        Guid branchId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        return await WithItems()
            .Where(attempt => attempt.BranchId == branchId && attempt.SessionId == sessionId)
            .OrderBy(attempt => attempt.AttemptNumber)
            .ToListAsync(ct);
    }

    private IQueryable<SessionCountAttempt> WithItems()
    {
        return dbContext.SessionCountAttempts
            .AsNoTracking()
            .Include(attempt => attempt.Items)
            .ThenInclude(item => item.Product);
    }
}

internal class OpeningCountsRepository(ServerDbContext dbContext) : IOpeningCountsRepository
{
    public async Task Add(OpeningCount openingCount, CancellationToken ct = default)
    {
        await dbContext.OpeningCounts.AddAsync(openingCount, ct);
    }

    public Task<OpeningCount?> GetCurrentByAccountId(Guid branchId, Guid accountId, CancellationToken ct = default)
    {
        return dbContext.OpeningCounts
            .Include(openingCount => openingCount.Items)
            .FirstOrDefaultAsync(openingCount =>
                openingCount.BranchId == branchId && openingCount.AccountId == accountId && openingCount.Active,
                ct);
    }

    public Task<OpeningCount?> GetCurrentByAccountIdAsNoTracking(Guid branchId, Guid accountId, CancellationToken ct = default)
    {
        return dbContext.OpeningCounts
            .AsNoTracking()
            .Include(openingCount => openingCount.Items)
            .FirstOrDefaultAsync(openingCount =>
                openingCount.BranchId == branchId && openingCount.AccountId == accountId && openingCount.Active,
                ct);
    }
}

internal class CustodyMappingIssuesRepository(ServerDbContext dbContext) : ICustodyMappingIssuesRepository
{
    public async Task<IReadOnlyList<CustodyMappingIssueCountRow>> CountUnresolvedByBranchIdAsNoTracking(
        Guid branchId,
        CancellationToken ct = default)
    {
        var rows = await dbContext.CustodyMappingIssues
            .AsNoTracking()
            .Where(issue => issue.BranchId == branchId && issue.Status == CustodyMappingIssueStatus.Unresolved)
            .GroupBy(issue => issue.IssueCode)
            .Select(group => new { IssueCode = group.Key, Count = group.Count() })
            .OrderBy(row => row.IssueCode)
            .ToListAsync(ct);

        return rows.Select(row => new CustodyMappingIssueCountRow(row.IssueCode, row.Count)).ToList();
    }
}
