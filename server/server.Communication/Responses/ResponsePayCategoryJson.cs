using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponsePayCategoryJson
{
    public Guid Id { get; set; }
    public DateTime CreatedAt { get; set; }

    public Guid BranchId { get; set; }
    public Guid ActorUserId { get; set; }
    public DateTime PostedOn { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public uint Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public PayItemKind Kind { get; set; }
    public bool Enabled { get; set; } = true;
    public Guid? PreviousId { get; set; }
}
