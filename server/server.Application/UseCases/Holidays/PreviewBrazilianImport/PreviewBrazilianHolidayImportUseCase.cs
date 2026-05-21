using server.Application.Services.Holidays;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Holidays.PreviewBrazilianImport;

public class PreviewBrazilianHolidayImportUseCase(
    IAuthenticationService authenticationService,
    IHolidaysRepository holidaysRepository,
    IBrazilianHolidayCalendar brazilianHolidayCalendar)
{
    public async Task<ResponseBrazilianHolidayPreviewJson> Execute(int year, bool includeOptionalFederal)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var entries = GetCalendarEntries(year, includeOptionalFederal);
        var existingDates = (await holidaysRepository.ListActiveDatesByBranchIdAndYearAsNoTracking(
                branchUser.BranchId,
                year))
            .ToHashSet();

        return new ResponseBrazilianHolidayPreviewJson
        {
            Year = year,
            IncludesOptionalFederal = includeOptionalFederal,
            Items = entries
                .Select(entry => new ResponseBrazilianHolidayPreviewItemJson
                {
                    Date = entry.Date,
                    Description = entry.Description,
                    Type = entry.Type,
                    AlreadyExists = existingDates.Contains(entry.Date)
                })
                .ToList()
        };
    }

    private IReadOnlyList<BrazilianHolidayEntry> GetCalendarEntries(int year, bool includeOptionalFederal)
    {
        try
        {
            return brazilianHolidayCalendar.GetForYear(year, includeOptionalFederal);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new OnValidationException([ResourcesErrorMessages.HOLIDAY_IMPORT_YEAR_OUT_OF_RANGE]);
        }
    }
}
