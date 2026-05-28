namespace server.Domain.Models;

public class FiadoBalanceQuery
{
    public Guid? ClientId { get; set; }
    public DateTime AsOfDate { get; set; }
}
