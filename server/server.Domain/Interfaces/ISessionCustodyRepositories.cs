using server.Domain.Entities;

namespace server.Domain.Interfaces;

public interface ITerminalsRepository
{
    Task Add(Terminal terminal, CancellationToken ct = default);
    Task<Terminal?> GetActiveByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<Terminal>> ListActiveByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);
}

public interface ISessionOpeningsRepository
{
    Task Add(SessionOpening opening, CancellationToken ct = default);
}

public interface ISessionHandoversRepository
{
    Task Add(SessionHandover handover, CancellationToken ct = default);
    Task<SessionHandover?> GetByIdAndBranchId(Guid id, Guid branchId, CancellationToken ct = default);
    Task<SessionHandover?> GetByIdAndBranchIdAsNoTracking(Guid id, Guid branchId, CancellationToken ct = default);

    /// <summary>The Offered or Disputed offer of the outgoing session, tracked, or null.</summary>
    Task<SessionHandover?> GetUnresolvedByOutgoingSessionId(Guid branchId, Guid outgoingSessionId, CancellationToken ct = default);
    Task<bool> ExistsUnresolvedByAccountIdAsNoTracking(Guid branchId, Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionHandover>> ListUnresolvedByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);
    Task<IReadOnlyList<SessionHandover>> ListBySessionIdAsNoTracking(Guid branchId, Guid sessionId, CancellationToken ct = default);
}

public interface ISessionOpeningDiscrepanciesRepository
{
    Task Add(SessionOpeningDiscrepancy discrepancy, CancellationToken ct = default);
    Task AddAttribution(OpeningDiscrepancyAttributionEvent attribution, CancellationToken ct = default);

    /// <summary>Appends a comparison revision (with its items) to an already-persisted discrepancy.</summary>
    Task AddRevision(SessionOpeningDiscrepancyRevision revision, CancellationToken ct = default);
    Task<SessionOpeningDiscrepancy?> GetByIdAndBranchId(Guid id, Guid branchId, CancellationToken ct = default);
    Task<SessionOpeningDiscrepancy?> GetBySessionId(Guid branchId, Guid sessionId, CancellationToken ct = default);
    Task<SessionOpeningDiscrepancy?> GetBySessionIdAsNoTracking(Guid branchId, Guid sessionId, CancellationToken ct = default);
    Task<OpeningDiscrepancyAttributionEvent?> GetLatestAttributionAsNoTracking(Guid discrepancyId, CancellationToken ct = default);

    /// <summary>
    /// Discrepancies whose current comparison is nonzero and whose current revision carries no attribution — never
    /// attributed, or attributed on an earlier revision that a predecessor correction superseded (queue group).
    /// </summary>
    Task<IReadOnlyList<SessionOpeningDiscrepancy>> ListAwaitingAttributionByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);
}

public interface ISessionEventsRepository
{
    Task Add(SessionEvent sessionEvent, CancellationToken ct = default);
    Task<IReadOnlyList<SessionEvent>> ListBySessionIdAsNoTracking(Guid branchId, Guid sessionId, CancellationToken ct = default);
}

public interface IOpeningCountsRepository
{
    Task Add(OpeningCount openingCount, CancellationToken ct = default);

    /// <summary>The cashbox's current (active) opening count with items, tracked, or null.</summary>
    Task<OpeningCount?> GetCurrentByAccountId(Guid branchId, Guid accountId, CancellationToken ct = default);
    Task<OpeningCount?> GetCurrentByAccountIdAsNoTracking(Guid branchId, Guid accountId, CancellationToken ct = default);
}

public interface ICustodyMappingIssuesRepository
{
    Task<IReadOnlyList<CustodyMappingIssueCountRow>> CountUnresolvedByBranchIdAsNoTracking(Guid branchId, CancellationToken ct = default);
}

public sealed record CustodyMappingIssueCountRow(Entities.Enums.CustodyMappingIssueCode IssueCode, int Count);
