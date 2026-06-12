namespace server.Domain.Models.Projections;

public record CategoryTotal(
    Guid CategoryId,
    string CategoryName,
    int Count,
    decimal TotalIn,
    decimal TotalOut);
