using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseManagementOverviewAccountJson
{
    public Guid AccountId { get; init; }
    public string AccountName { get; init; } = string.Empty;
    public AccountType AccountType { get; init; }
    public bool IsActive { get; init; }
    public decimal TotalIn { get; init; }
    public decimal TotalOut { get; init; }

    /// <summary><see cref="TotalIn"/> − <see cref="TotalOut"/> of this account only.</summary>
    public decimal Net { get; init; }
    public int ActiveCount { get; init; }

    /// <summary>Draft rows dated today, counted apart and never in the sums.</summary>
    public int DraftCount { get; init; }

    /// <summary>The latest date with an Active row, on or before today; null when the account never had one.</summary>
    public DateTime? LastActivityDate { get; init; }
}
