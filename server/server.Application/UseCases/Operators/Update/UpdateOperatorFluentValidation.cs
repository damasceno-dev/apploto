using FluentValidation;
using server.Communication.Requests;

namespace server.Application.UseCases.Operators.Update;

public class UpdateOperatorFluentValidation : AbstractValidator<RequestUpdateOperatorJson>
{
    public UpdateOperatorFluentValidation()
    {
        RuleFor(request => request.Name).ValidateOperatorName();
        RuleFor(request => request.UserId).ValidateOptionalUserId();
    }
}
