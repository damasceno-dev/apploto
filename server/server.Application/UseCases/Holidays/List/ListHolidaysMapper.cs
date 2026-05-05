using server.Communication.Requests;
using server.Domain.Models;

namespace server.Application.UseCases.Holidays.List;

public static class ListHolidaysMapper
{
    extension(RequestListHolidaysJson request)
    {
        public HolidayListFilter ToFilter()
        {
            return new HolidayListFilter
            {
                Year = request.Year,
                DateFrom = request.DateFrom,
                DateTo = request.DateTo,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
    }
}
