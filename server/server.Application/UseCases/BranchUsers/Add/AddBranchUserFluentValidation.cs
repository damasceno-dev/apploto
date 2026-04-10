using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.BranchUsers.Add;

public class AddBranchUserFluentValidation : AbstractValidator<RequestAddBranchUserJson>
{
    public AddBranchUserFluentValidation()
    {
        RuleFor(request => request)
            .Must(HasSingleUserReference)
            .WithMessage(ResourcesErrorMessages.BRANCH_USER_IDENTIFIER_REQUIRED)
            .Must(HasOnlyOneUserReference)
            .WithMessage(ResourcesErrorMessages.BRANCH_USER_IDENTIFIER_EXCLUSIVE);

        RuleFor(request => request.UserId)
            .NotEqual(Guid.Empty)
            .When(request => request.UserId.HasValue)
            .WithMessage(ResourcesErrorMessages.USER_ID_EMPTY);

        RuleFor(request => request.Email)
            .EmailAddress()
            .When(request => string.IsNullOrWhiteSpace(request.Email) is false)
            .WithMessage(ResourcesErrorMessages.EMAIL_INVALID);

        RuleFor(request => request.Role)
            .NotNull()
            .WithMessage(ResourcesErrorMessages.ROLE_REQUIRED);

        RuleFor(request => request.Role)
            .IsInEnum()
            .When(request => request.Role.HasValue)
            .WithMessage(ResourcesErrorMessages.ROLE_INVALID);
    }

    private static bool HasSingleUserReference(RequestAddBranchUserJson request)
    {
        return request.UserId.HasValue || string.IsNullOrWhiteSpace(request.Email) is false;
    }

    private static bool HasOnlyOneUserReference(RequestAddBranchUserJson request)
    {
        return request.UserId.HasValue is false || string.IsNullOrWhiteSpace(request.Email);
    }
}
