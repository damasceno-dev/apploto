using FluentValidation;
using server.Application.Services;
using server.Application.UseCases.Users;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Invitations.List;

public sealed class ListInvitationsFluentValidation : AbstractValidator<RequestListInvitationsJson>
{
    public ListInvitationsFluentValidation()
    {
        RuleFor(x => x.Status).IsInEnum().WithMessage(ResourcesErrorMessages.SECURITY_LIST_INVALID);
        RuleFor(x => x.Page).InclusiveBetween(1, 1000000).WithMessage(ResourcesErrorMessages.SECURITY_LIST_INVALID);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithMessage(ResourcesErrorMessages.SECURITY_LIST_INVALID);
    }
}
