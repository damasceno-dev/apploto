using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Holidays.Create;

public class CreateHolidaysUseCase(
    IAuthenticationService authenticationService,
    IHolidaysRepository holidaysRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseCreateHolidaysJson> Execute(RequestCreateHolidaysJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        EnsureNoDuplicateDatesInRequest(request);

        await EnsureNoConflictWithExistingHolidays(request, branchUser.BranchId);

        var holidays = request.Holidays
            .Select(row => row.ToDomain(branchUser.BranchId))
            .ToList();

        foreach (var holiday in holidays)
        {
            await holidaysRepository.Add(holiday);
        }

        await unitOfWork.Commit();

        return new ResponseCreateHolidaysJson
        {
            Items = holidays.Select(h => h.ToResponse()).ToList()
        };
    }

    private static void EnsureNoDuplicateDatesInRequest(RequestCreateHolidaysJson request)
    {
        var hasDuplicates = request.Holidays
            .GroupBy(row => DateOnly.FromDateTime(row.Date))
            .Any(g => g.Count() > 1);

        if (hasDuplicates)
            throw new ConflictException(ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT);
    }

    private async Task EnsureNoConflictWithExistingHolidays(
        RequestCreateHolidaysJson request,
        Guid branchId)
    {
        var existingDates = await holidaysRepository.ListActiveDatesByBranchIdAsNoTracking(branchId);
        var existingDateSet = existingDates.ToHashSet();

        var hasConflict = request.Holidays
            .Any(row => existingDateSet.Contains(DateOnly.FromDateTime(row.Date)));

        if (hasConflict)
            throw new ConflictException(ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT);
    }

    private static void Validate(RequestCreateHolidaysJson request)
    {
        var result = new CreateHolidaysFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
