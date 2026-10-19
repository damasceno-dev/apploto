using server.Application.UseCases.Platform.OperateBranch;
using server.Application.UseCases.Users.Google;
using server.Communication.Requests;
using Shouldly;
using Xunit;

namespace Validators.Test.AccountSecurity;

public sealed class M8PlatformAndGoogleValidatorsTest
{
    [Fact]
    public void OperatedOnboarding_ShouldRejectInvalidEmailNamesAndOutOfRangeAssignment()
    {
        var valid = new RequestOperateBranchJson
        {
            RequestId = Guid.NewGuid(), Name = "Lotérica", OwnerEmail = "owner@example.invalid",
            Terminals = [new RequestOnboardingTerminalJson { Name = "Terminal" }],
            Operators = [new RequestOnboardingOperatorJson { Name = "Operador", TerminalIndexes = [0] }]
        };
        new OperateBranchFluentValidation().Validate(valid).IsValid.ShouldBeTrue();
        var invalid = new RequestOperateBranchJson
        {
            RequestId = valid.RequestId, Name = " ", OwnerEmail = "bad-email",
            Terminals = valid.Terminals,
            Operators = [new RequestOnboardingOperatorJson { Name = "Operador", TerminalIndexes = [1] }]
        };
        new OperateBranchFluentValidation().Validate(invalid).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void GoogleSignIn_ShouldBoundUntrustedInputs_ButAcceptAWellShapedAssertion()
    {
        var validator = new GoogleSignInFluentValidation();
        validator.Validate(new RequestGoogleSignInJson
        { ChallengeId = Guid.NewGuid(), State = "state", IdToken = "signed-token" }).IsValid.ShouldBeTrue();
        validator.Validate(new RequestGoogleSignInJson
        { ChallengeId = Guid.Empty, State = new string('a', 129), IdToken = new string('x', 16385),
            InvitationToken = new string('b', 129) }).Errors.Count.ShouldBe(4);
    }
}
