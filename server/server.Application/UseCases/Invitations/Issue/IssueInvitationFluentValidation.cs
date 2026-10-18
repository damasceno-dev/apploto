using FluentValidation;
using server.Application.Services;
using server.Application.UseCases.Users;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Invitations.Issue;

public sealed class IssueInvitationFluentValidation : AbstractValidator<RequestIssueInvitationJson>
{
    public IssueInvitationFluentValidation()
    {
        RuleFor(x => x.Email).ValidateUserEmail();
        RuleFor(x => x.Role).IsInEnum().WithMessage(ResourcesErrorMessages.ROLE_INVALID);
        RuleFor(x => x.OperatorId).Must(id => !id.HasValue || id.Value != Guid.Empty).WithMessage(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
    }
}
