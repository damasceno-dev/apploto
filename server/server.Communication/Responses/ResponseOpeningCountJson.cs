namespace server.Communication.Responses;

public class ResponseOpeningCountJson
{
    public Guid Id { get; init; }
    public uint Version { get; init; }
    public Guid AccountId { get; init; }
    public DateTime AsOfDate { get; init; }
    public int Revision { get; init; }
    public Guid RecordedByUserId { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<ResponseSessionHandoverItemJson> Items { get; init; } = [];
}
