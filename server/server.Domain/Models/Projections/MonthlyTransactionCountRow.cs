using server.Domain.Entities.Enums;

namespace server.Domain.Models.Projections;

public record MonthlyTransactionCountRow(DateTime Date, TransactionStatus Status, int Count);
