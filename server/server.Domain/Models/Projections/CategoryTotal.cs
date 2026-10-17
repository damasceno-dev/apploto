namespace server.Domain.Models.Projections;

/// <summary>
/// One category of an operator summary. The <c>Generated*</c> members are the subset of the totals made of
/// system-generated Terminal legs (M7.7 Phase 5.8) — they are already inside <see cref="Count"/>,
/// <see cref="TotalIn"/> and <see cref="TotalOut"/>, never added on top.
/// </summary>
public record CategoryTotal(
    Guid CategoryId,
    string CategoryName,
    int Count,
    decimal TotalIn,
    decimal TotalOut,
    int GeneratedCount = 0,
    decimal GeneratedIn = 0m,
    decimal GeneratedOut = 0m);
