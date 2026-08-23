using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Transactions.Create;

public static class CreateTransactionMapper
{
    /// <summary>
    /// Builds the persisted <see cref="Transaction"/> for a single-create request. The
    /// denormalized <c>CategoryId</c> and <c>Direction</c> come from the resolved
    /// <paramref name="transactionType"/> — never from the request — so the classification
    /// invariant stays enforced at the mapping boundary.
    /// </summary>
    public static Transaction ToTransaction(
        this RequestCreateTransactionJson request,
        TransactionType transactionType,
        Guid recordedByOperatorId,
        DateTime dueDate,
        Guid createdByUserId,
        Guid branchId)
    {
        return new Transaction
        {
            Date = request.Date,
            Value = request.Value,
            Description = request.Description,
            TransactionTime = request.TransactionTime,
            TransactionTypeId = transactionType.Id,
            CategoryId = transactionType.CategoryId,
            Direction = transactionType.Category.DefaultDirection,
            AccountId = request.AccountId,
            ClientId = request.ClientId,
            DueDate = dueDate,
            RecordedByOperatorId = recordedByOperatorId,
            CreatedByUserId = createdByUserId,
            Status = request.SaveAsDraft ? TransactionStatus.Draft : TransactionStatus.Active,
            BranchId = branchId
        };
    }

    extension(Transaction transaction)
    {
        public ResponseCreateTransactionJson ToCreateResponse()
        {
            return new ResponseCreateTransactionJson
            {
                Id = transaction.Id,
                Version = transaction.Version,
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
                RecordedByOperatorId = transaction.RecordedByOperatorId,
                CreatedByUserId = transaction.CreatedByUserId,
                Status = transaction.Status,
                BranchId = transaction.BranchId,
                CreatedAt = transaction.CreatedAt
            };
        }
    }
}
