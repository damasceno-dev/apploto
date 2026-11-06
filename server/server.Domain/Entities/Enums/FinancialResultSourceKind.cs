namespace server.Domain.Entities.Enums;

public enum FinancialResultSourceKind
{
    Transaction = 0,
    CardSettlementBatchFee = 1,
    CardSettlementFeeEvidence = 2,
    OfficialSessionVariance = 3,
    ChequeCustody = 4,
    ChequeDeposit = 5,
    ChequeReturn = 6,
    ChequeConversion = 7,
    ProviderPrincipalSettlement = 8,
}
