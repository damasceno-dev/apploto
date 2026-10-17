using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Transactions;

public static class InstallmentPlanMapper
{
    extension(IReadOnlyList<Transaction> installments)
    {
        /// <summary>
        /// Draft when every installment is Draft; Cancelled when every one is Cancelled; Active when no Draft
        /// remains; otherwise Mixed (Draft alongside Active and/or Cancelled).
        /// </summary>
        public InstallmentPlanStatus ToPlanStatus()
        {
            var draftCount = installments.Count(installment => installment.Status == TransactionStatus.Draft);
            var cancelledCount = installments.Count(installment => installment.Status == TransactionStatus.Cancelled);

            if (draftCount == installments.Count)
                return InstallmentPlanStatus.Draft;
            if (cancelledCount == installments.Count)
                return InstallmentPlanStatus.Cancelled;
            return draftCount == 0 ? InstallmentPlanStatus.Active : InstallmentPlanStatus.Mixed;
        }

        public ResponseInstallmentPlanJson ToPlanResponse(Guid originTransactionId)
        {
            if (installments.Count == 0)
                throw new InvalidOperationException("A plan response requires at least one installment.");

            return new ResponseInstallmentPlanJson
            {
                OriginTransactionId = originTransactionId,
                Status = installments.ToPlanStatus(),
                AccountId = installments[0].AccountId,
                Date = installments[0].Date,
                DraftCount = installments.Count(installment => installment.Status == TransactionStatus.Draft),
                ActiveCount = installments.Count(installment => installment.Status == TransactionStatus.Active),
                CancelledCount = installments.Count(installment => installment.Status == TransactionStatus.Cancelled),
                TotalValue = installments
                    .Where(installment => installment.Status != TransactionStatus.Cancelled)
                    .Sum(installment => installment.Value),
                Installments = installments.Select(installment => installment.ToTransactionResponse()).ToList()
            };
        }
    }
}
