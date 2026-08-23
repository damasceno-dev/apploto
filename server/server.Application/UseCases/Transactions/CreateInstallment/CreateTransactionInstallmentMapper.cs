using server.Application.UseCases.Transactions.Create;
using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Transactions.CreateInstallment;

public static class CreateTransactionInstallmentMapper
{
    extension(InstallmentPlanRow row)
    {
        internal Transaction ToTransaction(
            RequestCreateTransactionInstallmentJson request,
            TransactionType transactionType,
            Guid recordedByOperatorId,
            Guid createdByUserId)
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
                BranchId = row.BranchId
            };
        }
    }

    extension(IReadOnlyList<Transaction> transactions)
    {
        public ResponseCreateTransactionInstallmentJson ToCreateInstallmentResponse()
        {
            if (transactions.Count == 0)
                throw new InvalidOperationException("An installment response requires at least one transaction.");

            var installments = transactions
                .Select(transaction => transaction.ToCreateResponse())
                .ToList();
            var version = installments[0].Version;
            if (installments.Any(installment => installment.Version != version))
                throw new InvalidOperationException("Installments created as one plan must share the same version.");

            return new ResponseCreateTransactionInstallmentJson
            {
                Version = version,
                Installments = installments
            };
        }
    }
}
