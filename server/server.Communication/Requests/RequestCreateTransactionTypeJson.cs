using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestCreateTransactionTypeJson
{
    public Guid CategoryId { get; init; }
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
    /// Receivable classification (M11 Phase 4) read by upcoming settlements and the standalone-cheque part of
    /// open-cheque aging. Explicit configuration, independent of <see cref="SettlementRule"/>; omitted means
    /// <c>None</c>.
    /// </summary>
    public SettlementInstrument? SettlementInstrument { get; init; }

    /// <summary>
    /// Monthly financial report classification (M11 Phase 6): <c>BolaoServiceFee</c> needs an <c>In</c> category,
    /// <c>CommercialLeftover</c> an <c>Out</c> one. Omitted means <c>None</c>.
    /// </summary>
    public MonthlyFinancialRole? MonthlyFinancialRole { get; init; }
}
