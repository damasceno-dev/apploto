using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

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
                OpenedByUserId = close.OpenedByUserId,
                OpenedByUserName = close.OpenedByUser?.Name ?? string.Empty,
                RecordedByUserId = close.RecordedByUserId,
                RecordedByUserName = close.RecordedByUser?.Name,
                RecordedByOperatorId = close.RecordedByOperatorId,
                RecordedByOperatorName = close.RecordedByOperator?.Name,
                SubmittedByUserId = close.SubmittedByUserId,
                SubmittedByUserName = close.SubmittedByUser?.Name,
                SubmittedByOperatorId = close.SubmittedByOperatorId,
                SubmittedByOperatorName = close.SubmittedByOperator?.Name,
                SubmittedAt = close.SubmittedAt,
                ApprovedAt = close.ApprovedAt,
                ApprovedByUserId = close.ApprovedByUserId,
                ApprovedByUserName = close.ApprovedByUser?.Name,
                RejectionReason = close.RejectionReason,
                Notes = close.Notes,
                ItemsFirstRecordedAt = close.ItemsFirstRecordedAt,
                OpeningRecheckRequiredAt = close.OpeningRecheckRequiredAt,
                OpeningRecheckTriggeredByDailyCloseId = close.OpeningRecheckTriggeredByDailyCloseId,
                OpeningRecheckTriggeredByUserId = close.OpeningRecheckTriggeredByUserId,
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
                            // Reopened or recalled closes keep their old variance record in the database for audit history.
                            // However, while in Draft state, we hide this old variance (returning null) so it isn't mistaken
                            // for the new submission's actual variance.
                            ClosingValue = isCashVarianceProduct && close.Status == DailyCloseStatus.Draft
                                ? null
                                : closingValuesByProductId.TryGetValue(product.Id, out var closingValue)
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
