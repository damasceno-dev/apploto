namespace server.Communication.Responses;

public class ResponseOperatorJson
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public Guid? UserId { get; set; }
}
