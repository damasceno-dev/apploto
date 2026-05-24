using server.Application.Services.Holidays;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Holidays.ImportBrazilian;

public class ImportBrazilianHolidaysUseCase(
    IAuthenticationService authenticationService,
    IHolidaysRepository holidaysRepository,
    IBrazilianHolidayCalendarResolver brazilianHolidayCalendarResolver,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseBrazilianHolidayImportJson> Execute(
        int year,
        bool includeOptionalFederal,
        BrazilianHolidayCalendarSource source = BrazilianHolidayCalendarSource.Composite,
        CancellationToken cancellationToken = default)
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var entries = await GetCalendarEntries(year, includeOptionalFederal, source, cancellationToken);
        var existingDates = (await holidaysRepository.ListActiveDatesByBranchIdAndYearAsNoTracking(
                branchUser.BranchId,
                year))
            .ToHashSet();
        var items = new List<ResponseBrazilianHolidayImportItemJson>(entries.Count);
        var importedCount = 0;

        foreach (var entry in entries)
        {
            if (existingDates.Contains(entry.Date))
            {
                items.Add(ToImportItem(entry, BrazilianHolidayImportStatus.Skipped));
                continue;
            }

            await holidaysRepository.Add(new Holiday
            {
                Id = Guid.NewGuid(),
                Date = entry.Date.ToDateTime(TimeOnly.MinValue),
                Description = entry.Description,
                BranchId = branchUser.BranchId,
                Source = entry.Source
            });

            importedCount++;
            items.Add(ToImportItem(entry, BrazilianHolidayImportStatus.Imported));
        }

        if (importedCount > 0)
            await unitOfWork.Commit();

        return new ResponseBrazilianHolidayImportJson
        {
            Year = year,
            IncludesOptionalFederal = includeOptionalFederal,
            Source = source,
            Items = items,
            ImportedCount = importedCount,
            SkippedCount = items.Count - importedCount
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
            return await brazilianHolidayCalendarResolver.GetForYear(year,includeOptionalFederal,source,cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new OnValidationException([ResourcesErrorMessages.HOLIDAY_IMPORT_YEAR_OUT_OF_RANGE]);
        }
    }

    private static ResponseBrazilianHolidayImportItemJson ToImportItem(
        SourcedBrazilianHolidayEntry entry,
        BrazilianHolidayImportStatus status)
    {
        return new ResponseBrazilianHolidayImportItemJson
        {
            Date = entry.Date,
            Description = entry.Description,
            Type = entry.Type,
            Status = status,
            Source = entry.Source
        };
    }
}
