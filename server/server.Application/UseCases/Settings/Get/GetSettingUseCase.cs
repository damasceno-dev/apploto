using server.Communication.Responses;
using server.Domain.Interfaces;

namespace server.Application.UseCases.Settings.Get;

public class GetSettingUseCase(
    IAuthenticationService authenticationService,
    ISettingsRepository settingsRepository)
{
    public async Task<ResponseSettingJson> Execute()
    {
        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        var setting = await settingsRepository.GetByBranchIdAsNoTracking(branchUser.BranchId)
            ?? throw new InvalidOperationException($"Setting row missing for branch {branchUser.BranchId}.");

        return setting.ToResponse();
    }
}
