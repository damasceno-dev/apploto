namespace server.Domain.Entities.Enums;

/// <summary>
/// What a transaction type's rows are as a receivable (M11 Phase 4): explicit admin configuration on the type, never
/// inferred from its name, category, direction or <see cref="SettlementRule"/>. Posted rows freeze this classification; reports read the posting snapshot.
/// </summary>
public enum SettlementInstrument
{
    /// <summary>Not a tracked receivable; the default for every type until an admin or the fresh seed classifies it.</summary>
    None = 0,

    /// <summary>A cheque, settled at deposit (Q2); external bank arrival is confirmed separately.</summary>
    Cheque = 1,

    /// <summary>A credit-card sale owed by the acquirer until the payout lands (working answer SQ1).</summary>
    CreditCard = 2,

    /// <summary>A debit-card sale owed by the acquirer until the payout lands (working answer SQ1).</summary>
    DebitCard = 3,

    Pix = 4,
    MarketPlace = 5
}
