namespace server.Domain.Models;

public class OpenChequeAgingListFilter
{
    public Guid? AccountId { get; set; }
    public Guid? ClientId { get; set; }
    public DateTime AsOfDate { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
