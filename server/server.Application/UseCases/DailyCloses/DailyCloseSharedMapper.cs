using server.Application.Services.DailyCloses;
using server.Communication.Responses;
using server.Domain.Entities;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.UseCases.DailyCloses;

public static class DailyCloseSharedMapper
{
    extension(DailyClose close)
    {
        /// <summary>
        /// Maps a <see cref="DailyClose"/> that has full navigation properties loaded (e.g.
        /// from a tracked <c>GetByIdAndBranchId</c> or read-only <c>...AsNoTracking</c>
        /// query with Includes) to the rich response DTO.
        /// </summary>
        public ResponseDailyCloseJson ToResponse(
            Guid cashVarianceProductId,
            User? submittedByUserOverride = null,
            Operator? submittedByOperatorOverride = null)
        {
            return new ResponseDailyCloseJson
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
                SubmittedByUserName = submittedByUserOverride?.Name ?? close.SubmittedByUser?.Name,
                SubmittedByOperatorId = close.SubmittedByOperatorId,
                SubmittedByOperatorName = submittedByOperatorOverride?.Name ?? close.SubmittedByOperator?.Name,
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
                Items = close.Items
                    .Where(item => close.IsVisible(item, cashVarianceProductId))
                    .Select(item => item.ToItemResponse())
                    .ToList()
            };
        }

        /// <summary>
        /// Maps a newly-persisted <see cref="DailyClose"/> to the rich response. The caller
        /// supplies the already-resolved <paramref name="account"/> and
        /// <paramref name="openedByUser"/> so navigation properties do not need to be
        /// populated on the entity.
        /// </summary>
        public ResponseDailyCloseJson ToResponse(
            Account account,
            User openedByUser)
        {
            return new ResponseDailyCloseJson
            {
                Id = close.Id,
                Version = close.Version,
                Date = close.Date,
                Status = close.Status,
                AccountId = close.AccountId,
                AccountName = account.Name,
                BranchId = close.BranchId,
                OpenedByUserId = close.OpenedByUserId,
                OpenedByUserName = openedByUser.Name,
                RecordedByUserId = null,
                RecordedByUserName = null,
                RecordedByOperatorId = null,
                RecordedByOperatorName = null,
                SubmittedByUserId = null,
                SubmittedByUserName = null,
                SubmittedByOperatorId = close.SubmittedByOperatorId,
                SubmittedByOperatorName = null,
                SubmittedAt = close.SubmittedAt,
                ApprovedAt = close.ApprovedAt,
                ApprovedByUserId = close.ApprovedByUserId,
                ApprovedByUserName = null,
                RejectionReason = close.RejectionReason,
                Notes = close.Notes,
                ItemsFirstRecordedAt = close.ItemsFirstRecordedAt,
                OpeningRecheckRequiredAt = close.OpeningRecheckRequiredAt,
                OpeningRecheckTriggeredByDailyCloseId = close.OpeningRecheckTriggeredByDailyCloseId,
                OpeningRecheckTriggeredByUserId = close.OpeningRecheckTriggeredByUserId,
                CreatedAt = close.CreatedAt,
                UpdatedAt = close.UpdatedAt,
                UpdatedByUserId = close.UpdatedByUserId,
                Items = []
            };
        }

        /// <summary>
        /// Maps a <see cref="DailyClose"/> to a response DTO using a pre-resolved product map
        /// to supply product names for items that may not have their <c>Product</c> navigation
        /// loaded (e.g. items newly inserted within the current unit of work).
        /// DB-loaded items fall back to their populated <c>item.Product</c> navigation.
        /// </summary>
        public ResponseDailyCloseJson ToResponse(
            IReadOnlyDictionary<Guid, Product> productMap,
            Guid cashVarianceProductId,
            User? recordedByUserOverride = null,
            Operator? recordedByOperatorOverride = null)
        {
            return new ResponseDailyCloseJson
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
                RecordedByUserName = recordedByUserOverride?.Name ?? close.RecordedByUser?.Name,
                RecordedByOperatorId = close.RecordedByOperatorId,
                RecordedByOperatorName = recordedByOperatorOverride?.Name ?? close.RecordedByOperator?.Name,
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
                Items = close.Items
                    .Where(item => close.IsVisible(item, cashVarianceProductId))
                    .Select(item => item.ToItemResponse(productMap))
                    .ToList()
            };
        }

        private bool IsVisible(DailyCloseItem item, Guid cashVarianceProductId)
        {
            return item.Active &&
                   (close.Status != Domain.Entities.Enums.DailyCloseStatus.Draft ||
                    item.ProductId != cashVarianceProductId);
        }
    }

    extension(DailyCloseItem item)
    {
        private ResponseDailyCloseItemJson ToItemResponse()
        {
            return new ResponseDailyCloseItemJson
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = item.Product?.Name ?? string.Empty,
                Value = item.Value,
                CreatedAt = item.CreatedAt
            };
        }

        private ResponseDailyCloseItemJson ToItemResponse(IReadOnlyDictionary<Guid, Product> productMap)
        {
            // productMap covers payload products, including newly inserted items that have no
            // Product navigation loaded. Existing DB-loaded items fall back to their navigation.
            var productName = productMap.TryGetValue(item.ProductId, out var product)
                ? product.Name
                : item.Product?.Name ?? string.Empty;

            return new ResponseDailyCloseItemJson
            {
                Id = item.Id,
                ProductId = item.ProductId,
                ProductName = productName,
                Value = item.Value,
                CreatedAt = item.CreatedAt
            };
        }
    }
}
