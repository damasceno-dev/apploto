using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Holidays.Update;

public class UpdateHolidayFluentValidation : AbstractValidator<RequestUpdateHolidayJson>
{
    internal const int MaximumDescriptionLength = 500;

    public UpdateHolidayFluentValidation()
    {
        RuleFor(r => r.Description)
            .MaximumLength(MaximumDescriptionLength)
            .When(r => r.Description is not null)
            .WithMessage(string.Format(ResourcesErrorMessages.HOLIDAY_DESCRIPTION_MAX_LENGTH, MaximumDescriptionLength));
    }
}
