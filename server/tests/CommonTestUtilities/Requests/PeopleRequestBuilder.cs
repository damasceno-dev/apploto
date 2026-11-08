using server.Communication.Requests;
using server.Domain.Entities.Enums;
namespace CommonTestUtilities.Requests;

public static class PeopleRequestBuilder
{
    public static readonly DateTime September = new(2026, 9, 1);
    public static RequestPayCategoryJson Category(PayItemKind kind = PayItemKind.Addition) => new()
    {
        Name = kind == PayItemKind.Addition ? "Salário sintético" : "Vale sintético", Kind = kind,
        PostedOn = September, Reason = "Configuração explícita", Evidence = "Fixture sintética, sem regra legal presumida"
    };
    public static RequestCommissionRuleJson Rule(Guid person, decimal rate = 18.5m) => new()
    {
        OperatorId = person, EffectiveFrom = September, EffectiveTo = new DateTime(2026,9,30), RatePercent = rate,
        IncludeBolaoFee = true, IncludeSpecialFee = true, PostedOn = September,
        Reason = "Configuração explícita", Evidence = "Regra sintética aprovada para demonstração"
    };
    public static RequestPayItemJson Pay(Guid person, Guid category, decimal amount = 1000m, PayItemKind kind = PayItemKind.Addition) => new()
    {
        OperatorId = person, CategoryId = category, SourceMonth = September, SourceDate = September, Amount = amount, Kind = kind,
        DeductionPolicy = kind == PayItemKind.Deduction ? "Política sintética do responsável P-1: lançamento deliberado, sem vínculo com falta de caixa" : null,
        PostedOn = September, Reason = "Item deliberado de demonstração", Evidence = "Documento sintético de origem"
    };
    public static RequestHourBankEntryJson Hours(Guid person, HourBankEntryKind kind = HourBankEntryKind.Opening, decimal value = 10m) => new()
    {
        OperatorId = person, Kind = kind, SignedHours = value, SourceDate = September, PostedOn = September,
        ExternalIdentity = "synthetic-" + Guid.NewGuid().ToString("N"), Reason = "Saldo/lançamento explícito", Evidence = "Saldo sintético comprovado, sem pontos fabricados"
    };
}
