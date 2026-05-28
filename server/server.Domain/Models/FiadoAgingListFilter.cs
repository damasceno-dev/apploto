namespace server.Domain.Models;

public class FiadoAgingListFilter
{
    public Guid? ClientId { get; set; }
    public Guid? AccountId { get; set; }
    public DateTime AsOfDate { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
