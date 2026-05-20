using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Settings;

public static class SettingSharedMapper
{
    extension(Setting setting)
    {
        public ResponseSettingJson ToResponse() => new()
        {
            Id = setting.Id,
            LockDate = setting.LockDate,
            DailyTargetHours = setting.DailyTargetHours,
            LunchDeductionOver6H = setting.LunchDeductionOver6H,
            LunchDeductionOver4H = setting.LunchDeductionOver4H,
            BranchId = setting.BranchId,
            CreatedAt = setting.CreatedAt,
            Active = setting.Active
        };
    }
}
