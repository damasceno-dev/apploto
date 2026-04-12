using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Operators.Update;

public class UpdateOperatorFluentValidation : AbstractValidator<RequestUpdateOperatorJson>
{
    internal const int OperatorNameMaxLength = 255;

    public UpdateOperatorFluentValidation()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.NAME_EMPTY);

        RuleFor(request => request.Name)
            .MaximumLength(OperatorNameMaxLength)
            .When(request => string.IsNullOrWhiteSpace(request.Name) is false)
            .WithMessage(string.Format(ResourcesErrorMessages.OPERATOR_NAME_MAX_LENGTH, OperatorNameMaxLength));

        RuleFor(request => request.UserId)
            .NotEqual(Guid.Empty)
            .When(request => request.UserId.HasValue)
            .WithMessage(ResourcesErrorMessages.USER_ID_EMPTY);
    }
}
