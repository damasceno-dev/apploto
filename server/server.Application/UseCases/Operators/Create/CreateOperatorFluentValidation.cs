using FluentValidation;
using server.Communication.Requests;

namespace server.Application.UseCases.Operators.Create;

public class CreateOperatorFluentValidation : AbstractValidator<RequestCreateOperatorJson>
{
    public CreateOperatorFluentValidation()
    {
        RuleFor(request => request.Name).ValidateOperatorName();
        RuleFor(request => request.UserId).ValidateOptionalUserId();
    }
}
