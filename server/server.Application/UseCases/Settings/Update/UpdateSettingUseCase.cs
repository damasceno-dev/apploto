using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Settings.Update;

public class UpdateSettingUseCase(
    IAuthenticationService authenticationService,
    ISettingsRepository settingsRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseSettingJson> Execute(RequestUpdateSettingJson request)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var setting = await settingsRepository.GetByBranchId(branchUser.BranchId)
            ?? throw new InvalidOperationException($"Setting row missing for branch {branchUser.BranchId}.");

        if (request.LockDate is { } newLockDate && newLockDate < setting.LockDate)
            throw new OnValidationException([ResourcesErrorMessages.SETTING_LOCK_DATE_RETREAT]);

        if (request.LockDate.HasValue)
            setting.LockDate = request.LockDate.Value;

        if (request.DailyTargetHours.HasValue)
            setting.DailyTargetHours = request.DailyTargetHours.Value;

        if (request.LunchDeductionOver6H.HasValue)
            setting.LunchDeductionOver6H = request.LunchDeductionOver6H.Value;

        if (request.LunchDeductionOver4H.HasValue)
            setting.LunchDeductionOver4H = request.LunchDeductionOver4H.Value;

        await unitOfWork.Commit();

        return setting.ToResponse();
    }

    private static void Validate(RequestUpdateSettingJson request)
    {
        var result = new UpdateSettingFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
