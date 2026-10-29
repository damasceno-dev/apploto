namespace server.Domain.Models.Projections;

/// <summary>
/// Active rows of one monthly financial role on Terminal accounts, grouped by
/// <c>(Date, Account, recording operator, type)</c> with their inflow and outflow sums kept apart.
/// </summary>
public record MonthlyFinancialRoleRow(
    DateTime Date,
    Guid AccountId,
    string AccountName,
    bool AccountActive,
    Guid RecordedByOperatorId,
    string RecordedByOperatorName,
    bool RecordedByOperatorActive,
    Guid TransactionTypeId,
    string TransactionTypeName,
    bool TransactionTypeActive,
    decimal InTotal,
    decimal OutTotal,
    int RowCount,
    Guid? SessionId = null);
