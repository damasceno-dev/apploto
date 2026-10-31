using FluentValidation;
using server.Application.Services;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;

namespace server.Application.UseCases.DailyCloses;

/// <summary>
/// Shape of one close-form field on the count save (M9.6 Phase 7): a defined field, a value that is present, within
/// numeric(14,2) in both directions with at most two decimal places (never rounded), and nonnegative for the fees.
/// </summary>
internal sealed class SessionCloseFieldFluentValidation : AbstractValidator<RequestSessionCloseFieldJson>
{
    public SessionCloseFieldFluentValidation()
    {
        RuleFor(field => field.Field)
            .IsInEnum()
            .WithMessage(ResourcesErrorMessages.SESSION_CLOSE_FIELD_INVALID);

        RuleFor(field => field.Value)
            .NotNull()
            .WithMessage(ResourcesErrorMessages.SESSION_CLOSE_FIELD_VALUE_REQUIRED);

        When(field => field.Value is not null, () =>
        {
            RuleFor(field => field.Value!.Value)
                .Must(value => decimal.Round(value, 2) == value &&
                               value < SharedValidators.Numeric14X2UpperBound &&
                               value > -SharedValidators.Numeric14X2UpperBound)
                .WithMessage(ResourcesErrorMessages.SESSION_CLOSE_FIELD_VALUE_PRECISION);

            RuleFor(field => field.Value!.Value)
                .GreaterThanOrEqualTo(0m)
                .When(field => field.Field != SessionCloseField.TerminalReport)
                .WithMessage(ResourcesErrorMessages.SESSION_CLOSE_FIELD_FEE_NEGATIVE);
        });
    }
}
