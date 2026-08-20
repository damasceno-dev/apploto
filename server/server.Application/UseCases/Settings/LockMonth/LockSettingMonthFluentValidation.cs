using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Settings.LockMonth;

internal sealed class LockSettingMonthFluentValidation : AbstractValidator<RequestLockSettingMonthJson>
{
    internal const int MinimumYear = 2000;
    internal const int MaximumYear = 2100;

    public LockSettingMonthFluentValidation()
    {
        RuleFor(request => request.Year)
            .InclusiveBetween(MinimumYear, MaximumYear)
            .WithMessage(string.Format(ResourcesErrorMessages.SETTING_LOCK_MONTH_YEAR_OUT_OF_RANGE, MinimumYear, MaximumYear));

        RuleFor(request => request.Month)
            .InclusiveBetween(1, 12)
            .WithMessage(ResourcesErrorMessages.SETTING_LOCK_MONTH_INVALID);
    }
}
