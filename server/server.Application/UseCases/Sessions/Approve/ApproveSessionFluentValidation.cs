using FluentValidation;
using server.Application.Services.Sessions;
using server.Communication.Requests;
using server.Exceptions;

namespace server.Application.UseCases.Sessions.Approve;

public class ApproveSessionFluentValidation : AbstractValidator<RequestApproveSessionJson>
{
    public ApproveSessionFluentValidation()
    {
        RuleFor(request => request.Mode).IsInEnum().WithMessage(ResourcesErrorMessages.SESSION_APPROVAL_MODE_INVALID);
        RuleFor(request => request.Reason)
            .MaximumLength(SessionApprovalPolicy.ReasonMaxLength)
            .WithMessage(ResourcesErrorMessages.SESSION_APPROVAL_REASON_TOO_LONG);
    }
}

public class ApproveSessionBatchFluentValidation : AbstractValidator<RequestApproveSessionBatchJson>
{
    public const int MaximumSessions = 100;

    public ApproveSessionBatchFluentValidation()
    {
        RuleFor(request => request.Mode).IsInEnum().WithMessage(ResourcesErrorMessages.SESSION_APPROVAL_MODE_INVALID);
        RuleFor(request => request.Reason)
            .MaximumLength(SessionApprovalPolicy.ReasonMaxLength)
            .WithMessage(ResourcesErrorMessages.SESSION_APPROVAL_REASON_TOO_LONG);
        RuleFor(request => request.Sessions)
            .NotEmpty()
            .WithMessage(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_EMPTY);
        RuleFor(request => request.Sessions)
            .Must(sessions => sessions is null || sessions.Count <= MaximumSessions)
            .WithMessage(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_TOO_LARGE);
        RuleFor(request => request.Sessions)
            .Must(sessions => sessions is null || sessions.Select(item => item.SessionId).Distinct().Count() == sessions.Count)
            .WithMessage(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_DUPLICATE);
        RuleForEach(request => request.Sessions)
            .Must(item => item.SessionId != Guid.Empty)
            .WithMessage(ResourcesErrorMessages.SESSION_NOT_FOUND);
    }
}
