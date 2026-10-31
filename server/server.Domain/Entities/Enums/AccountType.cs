namespace server.Domain.Entities.Enums;

public enum AccountType
{
    Terminal = 0,
    BankAccount = 1,
    Tab = 2,

    /// <summary>Cofre (M9.6 Phase 9): custody of cash outside the cashboxes; no session, count or variance.</summary>
    Safe = 3
}
