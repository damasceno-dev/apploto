namespace server.Domain.Models.Projections;

public sealed record TerminalActivityPairRow(DateTime Date, Guid AccountId, string AccountName);
