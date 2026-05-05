using server.Communication.Requests;
using server.Domain.Entities;

namespace server.Application.UseCases.Holidays.Create;

public static class CreateHolidaysMapper
{
    public static Holiday ToDomain(this RequestCreateHolidayJson row, Guid branchId)
    {
        return new Holiday
        {
            Id = Guid.NewGuid(),
            Date = row.Date,
            Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
            BranchId = branchId
        };
    }
}
