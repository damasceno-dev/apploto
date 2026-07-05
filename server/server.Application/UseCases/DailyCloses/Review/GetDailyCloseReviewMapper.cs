using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.DailyCloses.Review;

internal static class GetDailyCloseReviewMapper
{
    extension(DailyClose close)
    {
        public ResponseDailyCloseReviewJson ToReviewResponse(DailyClose? priorClose)
        {
            var priorValuesByProductId = priorClose?.Items
                .Where(item => item.Active)
                .ToDictionary(item => item.ProductId, item => item.Value)
                ?? [];

            return new ResponseDailyCloseReviewJson
            {
                Id = close.Id,
                Date = close.Date,
                Status = close.Status,
                AccountId = close.AccountId,
                AccountName = close.Account?.Name ?? string.Empty,
                BranchId = close.BranchId,
                SubmittedByOperatorId = close.SubmittedByOperatorId,
                SubmittedByOperatorName = close.SubmittedByOperator?.Name,
                SubmittedAt = close.SubmittedAt,
                ApprovedAt = close.ApprovedAt,
                ApprovedByUserId = close.ApprovedByUserId,
                ApprovedByUserName = close.ApprovedByUser?.Name,
                RejectionReason = close.RejectionReason,
                Notes = close.Notes,
                CreatedAt = close.CreatedAt,
                UpdatedAt = close.UpdatedAt,
                UpdatedByUserId = close.UpdatedByUserId,
                Items = close.Items
                    .Where(item => item.Active)
                    .OrderBy(item => item.Product?.Name, StringComparer.Ordinal)
                    .ThenBy(item => item.ProductId)
                    .Select(item =>
                    {
                        var isCashVarianceProduct = item.Product?.Name.Equals(
                            CashVarianceProductResolver.CashVarianceProductName,
                            StringComparison.Ordinal) is true;

                        return new ResponseDailyCloseReviewItemJson
                        {
                            ProductId = item.ProductId,
                            ProductName = item.Product?.Name ?? string.Empty,
                            OpeningValue = isCashVarianceProduct
                                ? null
                                : priorValuesByProductId.GetValueOrDefault(item.ProductId),
                            ClosingValue = item.Value,
                            IsCashVarianceProduct = isCashVarianceProduct
                        };
                    })
                    .ToList()
            };
        }
    }
}
