using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseCommissionRuleJson
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid BranchId { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime PostedOn { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public uint Version { get; set; }
    public Guid OperatorId { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    public decimal RatePercent { get; set; }
    public bool IncludeBolaoFee { get; set; }
    public bool IncludeSpecialFee { get; set; }
    public bool IncludeExplicitSources { get; set; }
    public bool Enabled { get; set; } = true;
    public Guid? PreviousId { get; set; }
}
