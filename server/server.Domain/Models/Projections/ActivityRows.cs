using server.Domain.Entities.Enums;

namespace server.Domain.Models.Projections;

/// <summary>
/// One account's day under the daily-ledger predicates (M11 Phase 7.2): <c>In</c>/<c>Out</c> sums and the count over
/// Active rows, with Draft rows counted apart and never summed.
/// </summary>
public record AccountActivityRow(
    Guid AccountId,
    string AccountName,
    AccountType AccountType,
    bool AccountActive,
    decimal TotalIn,
    decimal TotalOut,
    int ActiveCount,
    int DraftCount);

/// <summary>An account's latest business date carrying an Active row, on or before the asked date.</summary>
public record AccountLastActivityRow(Guid AccountId, DateTime LastActivityDate);

/// <summary>
/// One recording operator's rows under the operator-summary predicates (M11 Phase 7.3), with the generated share as a
/// subset of the totals (M7.7 Phase 5.8).
/// </summary>
public record OperatorActivityRow(
    Guid OperatorId,
    string OperatorName,
    bool OperatorActive,
    int TransactionCount,
    decimal TotalIn,
    decimal TotalOut,
    int GeneratedCount,
    decimal GeneratedIn,
    decimal GeneratedOut);
