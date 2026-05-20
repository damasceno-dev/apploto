using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Settings.Update;

internal class UpdateSettingFluentValidation : AbstractValidator<RequestUpdateSettingJson>
{
    public UpdateSettingFluentValidation()
    {
        When(x => x.DailyTargetHours.HasValue, () =>
        {
            RuleFor(x => x.DailyTargetHours!.Value)
                .GreaterThan(0)
                .WithMessage(ResourcesErrorMessages.SETTING_DAILY_TARGET_OUT_OF_RANGE);

            RuleFor(x => x.DailyTargetHours!.Value)
                .LessThanOrEqualTo(24)
                .WithMessage(ResourcesErrorMessages.SETTING_DAILY_TARGET_OUT_OF_RANGE);

            RuleFor(x => x.DailyTargetHours!.Value)
                .Must(v => decimal.Round(v, 2) == v)
                .WithMessage(ResourcesErrorMessages.SETTING_DAILY_TARGET_OUT_OF_RANGE);
        });

        When(x => x.LunchDeductionOver6H.HasValue, () =>
        {
            RuleFor(x => x.LunchDeductionOver6H!.Value)
                .GreaterThanOrEqualTo(0)
                .WithMessage(ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);

            RuleFor(x => x.LunchDeductionOver6H!.Value)
                .LessThanOrEqualTo(8)
                .WithMessage(ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);

            RuleFor(x => x.LunchDeductionOver6H!.Value)
                .Must(v => decimal.Round(v, 2) == v)
                .WithMessage(ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);
        });

        When(x => x.LunchDeductionOver4H.HasValue, () =>
        {
            RuleFor(x => x.LunchDeductionOver4H!.Value)
                .GreaterThanOrEqualTo(0)
                .WithMessage(ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);

            RuleFor(x => x.LunchDeductionOver4H!.Value)
                .LessThanOrEqualTo(8)
                .WithMessage(ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);

            RuleFor(x => x.LunchDeductionOver4H!.Value)
                .Must(v => decimal.Round(v, 2) == v)
                .WithMessage(ResourcesErrorMessages.SETTING_LUNCH_DEDUCTION_OUT_OF_RANGE);
        });
    }
}
