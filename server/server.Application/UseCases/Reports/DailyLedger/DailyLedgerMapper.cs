using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Reports.DailyLedger;

internal static class DailyLedgerMapper
{
    extension(Transaction transaction)
    {
        public ResponseDailyLedgerItemJson ToDailyLedgerItem()
        {
            return new ResponseDailyLedgerItemJson
            {
                Id = transaction.Id,
                Date = transaction.Date,
                Value = transaction.Value,
                Direction = transaction.Direction,
                Description = transaction.Description,
                TransactionTypeName = transaction.TransactionType.Name,
                CategoryName = transaction.Category.Name,
                ClientName = transaction.Client?.Name,
                RecordedByOperatorName = transaction.RecordedByOperator.Name,
                DueDate = transaction.DueDate,
                PaidAt = transaction.PaidAt
            };
        }
    }
}
