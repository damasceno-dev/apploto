using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Operator = server.Domain.Entities.Operator;

namespace server.Application.UseCases.DailyCloses.Open;

public static class OpenDailyCloseMapper
{
    /// <summary>
    /// Builds a new <see cref="DailyClose"/> entity from the request. Navigation properties
    /// are intentionally left unset so EF Core does not try to re-insert already-persisted
    /// related entities when the close is added to the DbContext.
    /// </summary>
    public static DailyClose ToDomain(
        this RequestOpenDailyCloseJson request,
        Guid branchId,
        Guid? submittedByOperatorId)
    {
        return new DailyClose
        {
            Date = request.Date,
            Status = DailyCloseStatus.Draft,
            AccountId = request.AccountId,
            BranchId = branchId,
            SubmittedByOperatorId = submittedByOperatorId
        };
    }

    extension(DailyClose close)
    {
        /// <summary>
        /// Maps a <see cref="DailyClose"/> that has full navigation properties loaded (e.g.
        /// from a tracked <c>GetByIdAndBranchId</c> or read-only <c>...AsNoTracking</c>
        /// query with Includes) to the rich response DTO.
        /// </summary>
        public ResponseDailyCloseJson ToResponse()
        {
            return new ResponseDailyCloseJson
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
                    .Select(item => item.ToItemResponse())
                    .ToList()
            };
        }

        /// <summary>
        /// Maps a newly-persisted <see cref="DailyClose"/> to the rich response. The caller
        /// supplies the already-resolved <paramref name="account"/> and
        /// <paramref name="submittedByOperator"/> so navigation properties do not need to be
        /// populated on the entity.
        /// </summary>
        public ResponseDailyCloseJson ToResponse(Account account,
            Operator? submittedByOperator)
        {
            return new ResponseDailyCloseJson
            {
                Id = close.Id,
                Date = close.Date,
                Status = close.Status,
                AccountId = close.AccountId,
                AccountName = account.Name,
                BranchId = close.BranchId,
                SubmittedByOperatorId = close.SubmittedByOperatorId,
                SubmittedByOperatorName = submittedByOperator?.Name,
                SubmittedAt = close.SubmittedAt,
                ApprovedAt = close.ApprovedAt,
                ApprovedByUserId = close.ApprovedByUserId,
                ApprovedByUserName = null,
                RejectionReason = close.RejectionReason,
                Notes = close.Notes,
                CreatedAt = close.CreatedAt,
                UpdatedAt = close.UpdatedAt,
                UpdatedByUserId = close.UpdatedByUserId,
                Items = []
            };
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
    }
}
