using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Branches.CreateSession;

public class CreateBranchSessionFluentValidation : AbstractValidator<RequestCreateBranchSessionJson>
{
    public CreateBranchSessionFluentValidation()
    {
        RuleFor(request => request.BranchId)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.BRANCH_ID_EMPTY);
    }
}
