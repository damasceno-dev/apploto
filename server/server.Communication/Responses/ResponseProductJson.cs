namespace server.Communication.Responses;

public class ResponseProductJson
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public Guid BranchId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool Active { get; set; }
}
