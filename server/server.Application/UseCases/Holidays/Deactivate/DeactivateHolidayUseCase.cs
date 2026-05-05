using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Holidays.Deactivate;

public class DeactivateHolidayUseCase(
    IAuthenticationService authenticationService,
    IHolidaysRepository holidaysRepository,
    IUnitOfWork unitOfWork)
{
    public async Task Execute(Guid holidayId)
    {
        if (holidayId == Guid.Empty)
            throw new OnValidationException([ResourcesErrorMessages.HOLIDAY_ID_EMPTY]);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var holiday = await holidaysRepository.GetByIdAndBranchId(holidayId, branchUser.BranchId)
            ?? throw new NotFoundException(ResourcesErrorMessages.HOLIDAY_NOT_FOUND);

        holiday.Active = false;

        await unitOfWork.Commit();
    }
}
