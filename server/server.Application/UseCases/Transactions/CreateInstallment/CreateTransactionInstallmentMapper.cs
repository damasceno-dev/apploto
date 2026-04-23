using server.Application.UseCases.Transactions.Create;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Transactions.CreateInstallment;

internal sealed record CreateTransactionInstallmentRow(
    Guid Id,
    Guid OriginTransactionId,
    decimal Value,
    DateTime DueDate,
    string Description);

public static class CreateTransactionInstallmentMapper
{
    internal static Transaction ToTransaction(
        this CreateTransactionInstallmentRow row,
        RequestCreateTransactionInstallmentJson request,
        TransactionType transactionType,
        Guid recordedByOperatorId,
        Guid createdByUserId,
        Guid branchId)
    {
        return new Transaction
        {
            Id = row.Id,
            Date = request.Date,
            Value = row.Value,
            Description = row.Description,
            TransactionTime = request.TransactionTime,
            TransactionTypeId = transactionType.Id,
            CategoryId = transactionType.CategoryId,
            Direction = transactionType.Category.DefaultDirection,
            AccountId = request.AccountId,
            ClientId = request.ClientId,
            DueDate = row.DueDate,
            OriginTransactionId = row.OriginTransactionId,
            RecordedByOperatorId = recordedByOperatorId,
            CreatedByUserId = createdByUserId,
            Status = request.SaveAsDraft ? TransactionStatus.Draft : TransactionStatus.Active,
            BranchId = branchId
        };
    }

    extension(IReadOnlyList<Transaction> transactions)
    {
        public ResponseCreateTransactionInstallmentJson ToCreateInstallmentResponse()
        {
            return new ResponseCreateTransactionInstallmentJson
            {
                Installments = transactions
                    .Select(transaction => transaction.ToCreateResponse())
                    .ToList()
            };
        }
    }
}
