using server.Domain.Entities.Enums;

namespace server.Domain.Models.Projections;

public record VarianceTimeSeriesRow(
    DateTime Date,
    Guid AccountId,
    string AccountName,
    decimal Value,
    DailyCloseStatus DailyCloseStatus,
    Guid DailyCloseId);
