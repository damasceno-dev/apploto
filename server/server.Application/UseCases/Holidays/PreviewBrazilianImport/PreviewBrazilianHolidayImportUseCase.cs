using server.Application.Services.Holidays;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Holidays.PreviewBrazilianImport;

public class PreviewBrazilianHolidayImportUseCase(
    IAuthenticationService authenticationService,
    IHolidaysRepository holidaysRepository,
    IBrazilianHolidayCalendarResolver brazilianHolidayCalendarResolver)
{
    public async Task<ResponseBrazilianHolidayPreviewJson> Execute(
        int year,
        bool includeOptionalFederal,
        BrazilianHolidayCalendarSource source = BrazilianHolidayCalendarSource.Composite,
        CancellationToken cancellationToken = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();
        var entries = await GetCalendarEntries(year, includeOptionalFederal, source, cancellationToken);
        var existingDates = (await holidaysRepository.ListActiveDatesByBranchIdAndYearAsNoTracking(
                branchUser.BranchId,
                year))
            .ToHashSet();

        return new ResponseBrazilianHolidayPreviewJson
        {
            Year = year,
            IncludesOptionalFederal = includeOptionalFederal,
            Source = source,
            Items = entries
                .Select(entry => new ResponseBrazilianHolidayPreviewItemJson
                {
                    Date = entry.Date,
                    Description = entry.Description,
                    Type = entry.Type,
                    AlreadyExists = existingDates.Contains(entry.Date),
                    Source = entry.Source
                })
                .ToList()
        };
    }

    private async Task<IReadOnlyList<SourcedBrazilianHolidayEntry>> GetCalendarEntries(
        int year,
        bool includeOptionalFederal,
        BrazilianHolidayCalendarSource source,
        CancellationToken cancellationToken)
    {
        try
        {
            return await brazilianHolidayCalendarResolver.GetForYear(
                year,
                includeOptionalFederal,
                source,
                cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new OnValidationException([ResourcesErrorMessages.HOLIDAY_IMPORT_YEAR_OUT_OF_RANGE]);
        }
    }
}
