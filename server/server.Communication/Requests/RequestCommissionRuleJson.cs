using server.Domain.Entities.Enums;

namespace server.Communication.Requests;

public class RequestCommissionRuleJson : RequestPeopleEvidenceJson
{
    public Guid OperatorId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public required decimal RatePercent { get; set; }
    public bool IncludeBolaoFee { get; set; }
    public bool IncludeSpecialFee { get; set; }
    public bool IncludeExplicitSources { get; set; }
    public bool Enabled { get; set; } = true;
}
