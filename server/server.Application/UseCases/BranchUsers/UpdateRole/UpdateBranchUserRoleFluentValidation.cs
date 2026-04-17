using FluentValidation;
using server.Communication.Requests;

namespace server.Application.UseCases.BranchUsers.UpdateRole;

public class UpdateBranchUserRoleFluentValidation : AbstractValidator<RequestUpdateBranchUserRoleJson>
{
    public UpdateBranchUserRoleFluentValidation()
    {
        RuleFor(request => request.Role).ValidateBranchUserRole();
    }
}
