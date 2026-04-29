using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Models;

namespace server.Application.UseCases.DailyCloses.List;

public static class ListDailyClosesMapper
{
    extension(RequestListDailyClosesJson request)
    {
        public DailyCloseListFilter ToFilter()
        {
            return new DailyCloseListFilter
            {
                AccountId = request.AccountId,
                Status = request.Status,
                DateFrom = request.DateFrom,
                DateTo = request.DateTo,
                OperatorId = request.OperatorId,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
    }

    extension(DailyClose close)
    {
        private ResponseListDailyCloseItemJson ToListItemResponse()
        {
            return new ResponseListDailyCloseItemJson
            {
                Id = close.Id,
                Date = close.Date,
                Status = close.Status,
                AccountId = close.AccountId,
                AccountName = close.Account.Name,
                SubmittedByOperatorId = close.SubmittedByOperatorId,
                SubmittedByOperatorName = close.SubmittedByOperator?.Name,
                ApprovedByUserId = close.ApprovedByUserId,
                ApprovedByUserName = close.ApprovedByUser?.Name,
                SubmittedAt = close.SubmittedAt,
                ApprovedAt = close.ApprovedAt,
                CreatedAt = close.CreatedAt
            };
        }
    }

    extension(IEnumerable<DailyClose> closes)
    {
        public ResponseListDailyClosesJson ToListResponse(DailyCloseListFilter filter, int totalCount)
        {
            var totalPages = filter.PageSize > 0
                ? (int)Math.Ceiling(totalCount / (double)filter.PageSize)
                : 0;

            return new ResponseListDailyClosesJson
            {
                Items = closes.Select(close => close.ToListItemResponse()).ToList(),
                Page = filter.Page,
                PageSize = filter.PageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNext = filter.Page < totalPages,
                HasPrevious = filter.Page > 1
            };
        }
    }
}
