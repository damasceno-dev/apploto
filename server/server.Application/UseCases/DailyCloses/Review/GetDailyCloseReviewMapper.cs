using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.DailyCloses.Review;

internal static class GetDailyCloseReviewMapper
{
    extension(DailyClose close)
    {
        public ResponseDailyCloseReviewJson ToReviewResponse(
            DailyClose? priorClose,
            IReadOnlyList<Product> activeProducts,
            Guid cashVarianceProductId)
        {
            var priorValuesByProductId = priorClose?.Items
                .Where(item => item.Active)
                .ToDictionary(item => item.ProductId, item => item.Value)
                ?? [];
            var closingValuesByProductId = close.Items
                .Where(item => item.Active)
                .ToDictionary(item => item.ProductId, item => item.Value);
            var productsToReview = activeProducts
                .Concat(close.Items
                    .Where(item => item.Active)
                    .Select(item => item.Product))
                .DistinctBy(product => product.Id)
                .OrderBy(product => product.DisplayOrder)
                .ThenBy(product => product.Id);

            return new ResponseDailyCloseReviewJson
            {
                Id = close.Id,
                Version = close.Version,
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
                Items = productsToReview
                    .Select(product =>
                    {
                        var isCashVarianceProduct = product.Id == cashVarianceProductId;

                        return new ResponseDailyCloseReviewItemJson
                        {
                            ProductId = product.Id,
                            ProductName = product.Name,
                            DisplayOrder = product.DisplayOrder,
                            OpeningValue = isCashVarianceProduct
                                ? null
                                : priorValuesByProductId.GetValueOrDefault(product.Id),
                            ClosingValue = closingValuesByProductId.TryGetValue(product.Id, out var closingValue)
                                ? closingValue
                                : null,
                            IsCashVarianceProduct = isCashVarianceProduct
                        };
                    })
                    .ToList()
            };
        }
    }
}
