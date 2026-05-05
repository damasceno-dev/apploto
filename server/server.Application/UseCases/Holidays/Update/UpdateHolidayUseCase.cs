using server.Application.UseCases.Holidays;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Holidays.Update;

public class UpdateHolidayUseCase(
    IAuthenticationService authenticationService,
    IHolidaysRepository holidaysRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseHolidayJson> Execute(Guid holidayId, RequestUpdateHolidayJson request)
    {
        Validate(holidayId, request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var holiday = await holidaysRepository.GetByIdAndBranchId(holidayId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.HOLIDAY_NOT_FOUND);

        holiday.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        await unitOfWork.Commit();

        return holiday.ToResponse();
    }

    private static void Validate(Guid holidayId, RequestUpdateHolidayJson request)
    {
        if (holidayId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.HOLIDAY_ID_EMPTY]);

        var result = new UpdateHolidayFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
