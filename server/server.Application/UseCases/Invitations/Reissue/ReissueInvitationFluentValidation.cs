using FluentValidation;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Invitations.Reissue;

public sealed class ReissueInvitationFluentValidation : AbstractValidator<RequestReissueInvitationJson>
{
    public ReissueInvitationFluentValidation()
    {
        RuleFor(x => x.OperatorId).Must(id => !id.HasValue || id.Value != Guid.Empty).WithMessage(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
    }
}
