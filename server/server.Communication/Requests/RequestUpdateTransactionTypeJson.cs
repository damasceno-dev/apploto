using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestUpdateTransactionTypeJson
{
    public string Name { get; init; } = string.Empty;
    public SettlementRule SettlementRule { get; init; }
    public bool RequiresTabAccountAndClient { get; init; }

    /// <summary>
    /// Marks the type as a fiado repayment method for <i>Receber pagamento</i> (M7.7 Phase 5.1): <c>CountedCash</c>
    /// also writes the generated Terminal cash entrada, <c>NonCash</c> writes the Tab entrada only. Only an
    /// <c>In</c>-category type that requires a Tab account and a client may carry it; omitted means not a method.
    /// </summary>
    public FiadoPaymentKind? FiadoPaymentKind { get; init; }

    /// <summary>
    /// Receivable classification (M11 Phase 4). Omitted keeps the current value, so a rename never clears it; an
    /// explicit <c>None</c> clears it. Applies to every row of the type at read time, past rows included.
    /// </summary>
    public SettlementInstrument? SettlementInstrument { get; init; }

    /// <summary>
    /// Monthly financial report classification (M11 Phase 6). Omitted keeps the current value; an explicit <c>None</c>
    /// clears it. Applies to every row of the type at read time, past and locked months included.
    /// </summary>
    public MonthlyFinancialRole? MonthlyFinancialRole { get; init; }
}
