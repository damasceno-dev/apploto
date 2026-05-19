using server.Domain.Entities.Enums;

namespace server.Communication.Responses;

public class ResponseCategoryJson
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Direction DefaultDirection { get; set; }
    public Guid BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Active { get; set; }
}
