using server.Application.UseCases.Invitations.Reissue;
using server.Application.UseCases.Invitations;
using server.Application.UseCases.Invitations.Issue;
using server.Application.UseCases.Invitations.List;
using server.Application.UseCases.Invitations.Register;
using server.Application.UseCases.OperatorLinkRequests.List;
using server.Application.UseCases.Users.ChangePassword;
using server.Application.UseCases.Users.ForgotPassword;
using server.Application.UseCases.Users.Logout;
using server.Application.UseCases.Users.ResetPassword;
using server.Application.UseCases.Users.UpdateProfile;
using server.Communication.Requests;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using Xunit;

namespace Validators.Test.AccountSecurity;

public sealed class AccountSecurityValidatorsTest
{
    private static readonly string Token = new('a', 43);

    [Fact]
    public void ValidContracts_ShouldPassWithoutRepositoryOrRoleDependencies()
    {
        new UpdateProfileFluentValidation().Validate(new RequestUpdateProfileJson { Name = "Pessoa" }).IsValid.ShouldBeTrue();
        new ChangePasswordFluentValidation().Validate(new RequestChangePasswordJson { CurrentPassword = "old-password", NewPassword = "Password123" }).IsValid.ShouldBeTrue();
        new LogoutFluentValidation().Validate(new RequestLogoutJson { RefreshToken = "refresh-value" }).IsValid.ShouldBeTrue();
        new ForgotPasswordFluentValidation().Validate(new RequestForgotPasswordJson { Email = "person@example.invalid" }).IsValid.ShouldBeTrue();
        new ResetPasswordFluentValidation().Validate(new RequestResetPasswordJson { Token = Token, NewPassword = "Password123" }).IsValid.ShouldBeTrue();
        new InvitationTokenFluentValidation().Validate(new RequestInvitationTokenJson { Token = Token }).IsValid.ShouldBeTrue();
        new RegisterInvitationFluentValidation().Validate(new RequestRegisterInvitationJson { Token = Token, Name = "Pessoa", Password = "Password123" }).IsValid.ShouldBeTrue();
        new IssueInvitationFluentValidation().Validate(new RequestIssueInvitationJson { Email = "person@example.invalid", Role = Role.Member }).IsValid.ShouldBeTrue();
        new ListInvitationsFluentValidation().Validate(new RequestListInvitationsJson()).IsValid.ShouldBeTrue();
        new ListOperatorLinkRequestsFluentValidation().Validate(new RequestListOperatorLinkRequestsJson()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Profile_ShouldRefuseEmptyName(string name)
    {
        new UpdateProfileFluentValidation().Validate(new RequestUpdateProfileJson { Name = name }).Errors
            .Select(e => e.ErrorMessage).ShouldContain(ResourcesErrorMessages.PROFILE_NAME_INVALID);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-email")]
    public void Recovery_ShouldRefuseMalformedEmail(string email)
    {
        new ForgotPasswordFluentValidation().Validate(new RequestForgotPasswordJson { Email = email }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Passwords_ShouldEnforceExistingMinimum_AndBcryptByteLimit()
    {
        new ChangePasswordFluentValidation().Validate(new RequestChangePasswordJson { CurrentPassword = "old", NewPassword = "short" }).IsValid.ShouldBeFalse();
        new ResetPasswordFluentValidation().Validate(new RequestResetPasswordJson { Token = Token, NewPassword = new string('é', 37) }).Errors
            .Select(e => e.ErrorMessage).ShouldContain(ResourcesErrorMessages.SECURITY_PASSWORD_MAX_LENGTH);
        new RegisterInvitationFluentValidation().Validate(new RequestRegisterInvitationJson { Token = Token, Name = "Pessoa", Password = "" }).Errors
            .Select(e => e.ErrorMessage).ShouldContain(ResourcesErrorMessages.PASSWORD_EMPTY);
    }

    [Fact]
    public void CurrentPasswordAndLogout_ShouldRequireTheirConfirmationCredential()
    {
        new ChangePasswordFluentValidation().Validate(new RequestChangePasswordJson { CurrentPassword = "", NewPassword = "Password123" }).Errors
            .Select(e => e.ErrorMessage).ShouldContain(ResourcesErrorMessages.SECURITY_CURRENT_PASSWORD_REQUIRED);
        new LogoutFluentValidation().Validate(new RequestLogoutJson()).Errors
            .Select(e => e.ErrorMessage).ShouldContain(ResourcesErrorMessages.SECURITY_REFRESH_TOKEN_REQUIRED);
    }

    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa+")]
    public void CapabilityValidators_ShouldRefuseMalformedToken(string token)
    {
        new InvitationTokenFluentValidation().Validate(new RequestInvitationTokenJson { Token = token }).IsValid.ShouldBeFalse();
        new ResetPasswordFluentValidation().Validate(new RequestResetPasswordJson { Token = token, NewPassword = "Password123" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Invitation_ShouldRejectUndefinedRoleAndEmptyOperatorId()
    {
        new IssueInvitationFluentValidation().Validate(new RequestIssueInvitationJson { Email = "person@example.invalid", Role = (Role)123 }).Errors
            .Select(e => e.ErrorMessage).ShouldContain(ResourcesErrorMessages.ROLE_INVALID);
        new IssueInvitationFluentValidation().Validate(new RequestIssueInvitationJson { Email = "person@example.invalid", OperatorId = Guid.Empty }).Errors
            .Select(e => e.ErrorMessage).ShouldContain(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(1000001, 50)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Lists_ShouldBoundPaging(int page, int size)
    {
        new ListInvitationsFluentValidation().Validate(new RequestListInvitationsJson { Page = page, PageSize = size }).IsValid.ShouldBeFalse();
        new ListOperatorLinkRequestsFluentValidation().Validate(new RequestListOperatorLinkRequestsJson { Page = page, PageSize = size }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Lists_ShouldValidateNullableEnums()
    {
        new ListInvitationsFluentValidation().Validate(new RequestListInvitationsJson { Status = (InvitationStatus)99 }).IsValid.ShouldBeFalse();
        new ListOperatorLinkRequestsFluentValidation().Validate(new RequestListOperatorLinkRequestsJson { Status = (OperatorLinkRequestStatus)99 }).IsValid.ShouldBeFalse();
    }
    [Fact]
    public void Reissue_ShouldAcceptClearAndValidReplacement_ButRejectEmptyOperatorId()
    {
        var validator = new ReissueInvitationFluentValidation();
        validator.Validate(new RequestReissueInvitationJson()).IsValid.ShouldBeTrue();
        validator.Validate(new RequestReissueInvitationJson { OperatorId = Guid.NewGuid() }).IsValid.ShouldBeTrue();
        validator.Validate(new RequestReissueInvitationJson { OperatorId = Guid.Empty }).Errors.Select(e => e.ErrorMessage)
            .ShouldContain(ResourcesErrorMessages.OPERATOR_ID_EMPTY);
    }
}
