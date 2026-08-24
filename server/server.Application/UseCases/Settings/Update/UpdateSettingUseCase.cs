using server.Application.Services.Transactions;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Settings.Update;

/// <summary>
/// Single write path for the branch time constants (§3.18/§6.7). The unique
/// <c>Setting</c> row mirrors the latest values (and stays the `If-Match`/ETag
/// concurrency root), while the effective-dated <c>TimeEntryPolicy</c> ledger is what
/// every calculation reads. A change lands on both in one commit: the policy row
/// effective from the branch-local day of the change is inserted, or mutated in place
/// when the same day already changed once. Days before that stay on their old policy,
/// so historical balances never move.
/// </summary>
public class UpdateSettingUseCase(
    IAuthenticationService authenticationService,
    ISettingsRepository settingsRepository,
    ITimeEntryPoliciesRepository timeEntryPoliciesRepository,
    IBranchClock branchClock,
    IUnitOfWork unitOfWork)
{
    public async Task<ResponseSettingJson> Execute(RequestUpdateSettingJson request, uint expectedVersion)
    {
        Validate(request);

        var branchUser = await authenticationService.GetAuthenticatedBranchUser();

        if (branchUser.Role is not Role.Admin and not Role.Manager)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);

        var setting = await settingsRepository.GetByBranchId(branchUser.BranchId)
            ?? throw new InvalidOperationException($"Setting row missing for branch {branchUser.BranchId}.");

        if (setting.Version != expectedVersion)
            throw new ConflictException(ResourcesErrorMessages.SETTING_STALE_WRITE);

        var policyChanged =
            (request.DailyTargetHours.HasValue && request.DailyTargetHours.Value != setting.DailyTargetHours) ||
            (request.LunchDeductionOver6H.HasValue && request.LunchDeductionOver6H.Value != setting.LunchDeductionOver6H) ||
            (request.LunchDeductionOver4H.HasValue && request.LunchDeductionOver4H.Value != setting.LunchDeductionOver4H);

        if (request.DailyTargetHours.HasValue)
            setting.DailyTargetHours = request.DailyTargetHours.Value;

        if (request.LunchDeductionOver6H.HasValue)
            setting.LunchDeductionOver6H = request.LunchDeductionOver6H.Value;

        if (request.LunchDeductionOver4H.HasValue)
            setting.LunchDeductionOver4H = request.LunchDeductionOver4H.Value;

        if (policyChanged)
            await UpsertEffectivePolicy(setting, branchUser.BranchId);

        await unitOfWork.Commit();

        return setting.ToResponse();
    }

    /// <summary>
    /// Records the mirrored constants as the policy effective from the branch-local day
    /// of the change. First change of the day inserts a new row; a repeat change on the
    /// same day mutates that day's row in place (the active unique
    /// <c>(BranchId, EffectiveFrom)</c> index keeps per-date resolution unambiguous).
    /// </summary>
    private async Task UpsertEffectivePolicy(Setting setting, Guid branchId)
    {
        var effectiveFrom = branchClock.LocalBusinessDate(branchClock.UtcNow());
        var sameDayPolicy = await timeEntryPoliciesRepository.GetActiveByBranchIdAndEffectiveFrom(
            branchId, effectiveFrom);

        if (sameDayPolicy is null)
        {
            await timeEntryPoliciesRepository.Add(new TimeEntryPolicy
            {
                BranchId = branchId,
                EffectiveFrom = effectiveFrom,
                DailyTargetHours = setting.DailyTargetHours,
                LunchDeductionOver6H = setting.LunchDeductionOver6H,
                LunchDeductionOver4H = setting.LunchDeductionOver4H
            });
            return;
        }

        sameDayPolicy.DailyTargetHours = setting.DailyTargetHours;
        sameDayPolicy.LunchDeductionOver6H = setting.LunchDeductionOver6H;
        sameDayPolicy.LunchDeductionOver4H = setting.LunchDeductionOver4H;
    }

    private static void Validate(RequestUpdateSettingJson request)
    {
        var result = new UpdateSettingFluentValidation().Validate(request);
        if (result.IsValid is false)
            throw new OnValidationException(result.Errors.Select(e => e.ErrorMessage).ToList());
    }
}
