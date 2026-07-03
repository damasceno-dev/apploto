namespace server.Domain.Models.Projections;

public record ExpectedCloserRow(Guid AccountId, string AccountName, Guid OperatorId, string OperatorName);
