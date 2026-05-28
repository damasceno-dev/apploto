namespace server.Domain.Models.Projections;

public record FiadoClientBalanceRow(Guid ClientId, string ClientName, decimal OutstandingTotal);
