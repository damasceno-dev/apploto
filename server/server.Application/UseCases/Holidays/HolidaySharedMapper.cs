using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Models;

namespace server.Application.UseCases.Holidays;

public static class HolidaySharedMapper
{
    extension(Holiday holiday)
    {
        public ResponseHolidayJson ToResponse()
        {
            return new ResponseHolidayJson
            {
                Id = holiday.Id,
                Date = holiday.Date,
                Description = holiday.Description,
                BranchId = holiday.BranchId,
                CreatedAt = holiday.CreatedAt,
                Active = holiday.Active
            };
        }
    }

    extension(IEnumerable<Holiday> holidays)
    {
        public ResponseListHolidaysJson ToListResponse(HolidayListFilter filter, int totalCount)
        {
            var totalPages = filter.PageSize > 0
                ? (int)Math.Ceiling(totalCount / (double)filter.PageSize)
                : 0;

            return new ResponseListHolidaysJson
            {
                Items = holidays.Select(h => h.ToResponse()).ToList(),
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
