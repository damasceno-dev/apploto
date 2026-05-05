using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Holidays.Create;

public class CreateHolidaysFluentValidation : AbstractValidator<RequestCreateHolidaysJson>
{
    internal const int MaximumDescriptionLength = 500;

    public CreateHolidaysFluentValidation()
    {
        RuleFor(r => r.Holidays)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.HOLIDAY_CREATE_BATCH_EMPTY)
            .Must(h => h.GroupBy(row => DateOnly.FromDateTime(row.Date)).All(g => g.Count() == 1))
            .WithMessage(ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT);

        RuleForEach(r => r.Holidays).ChildRules(row =>
        {
            row.RuleFor(r => r.Date)
                .NotEqual(default(DateTime))
                .WithMessage(ResourcesErrorMessages.HOLIDAY_DATE_REQUIRED);

            row.RuleFor(r => r.Description)
                .MaximumLength(MaximumDescriptionLength)
                .When(r => r.Description is not null)
                .WithMessage(string.Format(ResourcesErrorMessages.HOLIDAY_DESCRIPTION_MAX_LENGTH, MaximumDescriptionLength));
        });
    }
}
