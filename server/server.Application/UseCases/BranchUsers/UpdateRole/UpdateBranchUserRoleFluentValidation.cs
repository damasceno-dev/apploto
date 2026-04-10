using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.BranchUsers.UpdateRole;

public class UpdateBranchUserRoleFluentValidation : AbstractValidator<RequestUpdateBranchUserRoleJson>
{
    public UpdateBranchUserRoleFluentValidation()
    {
        RuleFor(request => request.Role)
            .NotNull()
            .WithMessage(ResourcesErrorMessages.ROLE_REQUIRED);

        RuleFor(request => request.Role)
            .IsInEnum()
            .When(request => request.Role.HasValue)
            .WithMessage(ResourcesErrorMessages.ROLE_INVALID);
    }
}
