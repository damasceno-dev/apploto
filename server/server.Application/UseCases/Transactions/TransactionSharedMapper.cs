using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Models;

namespace server.Application.UseCases.Transactions;

public static class TransactionSharedMapper
{
    extension(Transaction transaction)
    {
        public ResponseTransactionJson ToTransactionResponse()
        {
            return new ResponseTransactionJson
            {
                Id = transaction.Id,
                Date = transaction.Date,
                Value = transaction.Value,
                Description = transaction.Description,
                TransactionTime = transaction.TransactionTime,
                TransactionTypeId = transaction.TransactionTypeId,
                CategoryId = transaction.CategoryId,
                Direction = transaction.Direction,
                AccountId = transaction.AccountId,
                ClientId = transaction.ClientId,
                DueDate = transaction.DueDate,
                PaidAt = transaction.PaidAt,
                OriginTransactionId = transaction.OriginTransactionId,
                RecordedByOperatorId = transaction.RecordedByOperatorId,
                CreatedByUserId = transaction.CreatedByUserId,
                UpdatedAt = transaction.UpdatedAt,
                UpdatedByUserId = transaction.UpdatedByUserId,
                Status = transaction.Status,
                CancelledAt = transaction.CancelledAt,
                CancelledByUserId = transaction.CancelledByUserId,
                CancellationReason = transaction.CancellationReason,
                BranchId = transaction.BranchId,
                CreatedAt = transaction.CreatedAt
            };
        }

        private ResponseListTransactionItemJson ToListItemResponse()
        {
            return new ResponseListTransactionItemJson
            {
                Id = transaction.Id,
                Date = transaction.Date,
                Value = transaction.Value,
                Description = transaction.Description,
                Direction = transaction.Direction,
                Status = transaction.Status,
                AccountId = transaction.AccountId,
                AccountName = transaction.Account.Name,
                ClientId = transaction.ClientId,
                ClientName = transaction.Client?.Name,
                TransactionTypeId = transaction.TransactionTypeId,
                TransactionTypeName = transaction.TransactionType.Name,
                DueDate = transaction.DueDate,
                PaidAt = transaction.PaidAt,
                CreatedAt = transaction.CreatedAt
            };
        }
    }

    extension(IEnumerable<Transaction> transactions)
    {
        public ResponseListTransactionsJson ToListResponse(TransactionListFilter filter, int totalCount)
        {
            var totalPages = filter.PageSize > 0
                ? (int)Math.Ceiling(totalCount / (double)filter.PageSize)
                : 0;

            return new ResponseListTransactionsJson
            {
                Items = transactions.Select(t => t.ToListItemResponse()).ToList(),
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNext = filter.Page < totalPages,
                HasPrevious = filter.Page > 1
            };
        }
    }
    
    extension(RequestListTransactionsJson request)
    {
        public TransactionListFilter ToFilter()
        {
            return new TransactionListFilter
            {
                AccountId = request.AccountId,
                DateFrom = request.DateFrom,
                DateTo = request.DateTo,
                Status = request.Status,
                OperatorId = request.OperatorId,
                ClientId = request.ClientId,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
    }
}
