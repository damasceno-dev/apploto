using server.Application.Services.Sessions;
using server.Application.UseCases.Sessions.Approve;
using server.Application.UseCases.Sessions.Count;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.Sessions;

public class SessionCountAndApprovalFluentValidationTest
{
    [Fact]
    public void Conferir_ShouldSucceed_WithATokenAndRevisions()
    {
        new ConferSessionCountFluentValidation()
            .Validate(new RequestConferSessionCountJson { ConfirmationToken = Guid.NewGuid(), CountRevision = 2 })
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Conferir_ShouldFail_WithoutAToken()
    {
        var result = new ConferSessionCountFluentValidation().Validate(new RequestConferSessionCountJson());

        result.Errors.Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.SESSION_CONFIRMATION_TOKEN_REQUIRED]);
    }

    [Fact]
    public void Conferir_ShouldFail_WithANegativeRevision()
    {
        var result = new ConferSessionCountFluentValidation()
            .Validate(new RequestConferSessionCountJson { ConfirmationToken = Guid.NewGuid(), LedgerRevision = -1 });

        result.Errors.Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.SESSION_REVISION_INVALID]);
    }

    [Fact]
    public void Recount_ShouldFail_WithoutThePreviousAttempt()
    {
        var result = new BeginSessionRecountFluentValidation().Validate(new RequestBeginSessionRecountJson());

        result.Errors.Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.SESSION_CONFIRMATION_ATTEMPT_REQUIRED]);
    }

    [Fact]
    public void Submit_ShouldLeaveAMissingAttemptToTheStateCheck_ButRefuseANegativeRevision()
    {
        var validator = new SubmitSessionFluentValidation();

        validator.Validate(new RequestSubmitSessionJson()).IsValid.ShouldBeTrue();
        validator.Validate(new RequestSubmitSessionJson { OpeningRevision = -1 }).Errors
            .Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.SESSION_REVISION_INVALID]);
    }

    [Fact]
    public void Approve_ShouldFail_WithAnUndefinedModeOrAnOverlongReason()
    {
        var validator = new ApproveSessionFluentValidation();

        validator.Validate(new RequestApproveSessionJson { Mode = (SessionApprovalMode)9 }).Errors
            .Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.SESSION_APPROVAL_MODE_INVALID]);
        validator.Validate(new RequestApproveSessionJson { Reason = new string('x', SessionApprovalPolicy.ReasonMaxLength + 1) }).Errors
            .Select(error => error.ErrorMessage).ShouldBe([ResourcesErrorMessages.SESSION_APPROVAL_REASON_TOO_LONG]);
        validator.Validate(new RequestApproveSessionJson { Mode = SessionApprovalMode.SingleActiveManager, Reason = "Única" })
            .IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ApproveDay_ShouldRequireDistinctSessionsWithinTheLimit()
    {
        var validator = new ApproveSessionBatchFluentValidation();
        var id = Guid.NewGuid();

        validator.Validate(new RequestApproveSessionBatchJson()).Errors
            .Select(error => error.ErrorMessage).ShouldContain(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_EMPTY);
        validator.Validate(new RequestApproveSessionBatchJson { Sessions = [Item(id), Item(id)] }).Errors
            .Select(error => error.ErrorMessage).ShouldContain(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_DUPLICATE);
        validator.Validate(new RequestApproveSessionBatchJson
            {
                Sessions = [.. Enumerable.Range(0, ApproveSessionBatchFluentValidation.MaximumSessions + 1).Select(_ => Item(Guid.NewGuid()))]
            }).Errors
            .Select(error => error.ErrorMessage).ShouldContain(ResourcesErrorMessages.SESSION_BATCH_APPROVAL_TOO_LARGE);
        validator.Validate(new RequestApproveSessionBatchJson { Sessions = [Item(Guid.NewGuid()), Item(Guid.NewGuid())] })
            .IsValid.ShouldBeTrue();
    }

    private static RequestApproveSessionBatchItemJson Item(Guid sessionId) => new() { SessionId = sessionId, Version = 3 };
}
