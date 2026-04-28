using CommonTestUtilities.Entities;
using server.Application.Services.Members;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Members;

public class MemberAccountScopeGuardTest
{
    [Fact]
    public void EnsureMemberCanActOnAccount_ShouldNotThrow_WhenCallerIsNotMember()
    {
        var guard = new MemberAccountScopeGuard();
        var scope = new MemberAccountScope(LinkedOperator: null, AllowedAccountIds: []);

        Should.NotThrow(() => guard.EnsureMemberCanActOnAccount(Role.Manager, scope, Guid.NewGuid()));
    }

    [Fact]
    public void EnsureMemberCanActOnAccount_ShouldThrowRequiresOperatorLink_WhenMemberHasNoLinkedOperator()
    {
        var guard = new MemberAccountScopeGuard();
        var scope = new MemberAccountScope(LinkedOperator: null, AllowedAccountIds: []);

        var exception = Should.Throw<TokenWithoutPermissionException>(
            () => guard.EnsureMemberCanActOnAccount(Role.Member, scope, Guid.NewGuid()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
    }

    [Fact]
    public void EnsureMemberCanActOnAccount_ShouldThrowOutOfScope_WhenLinkedMemberTargetsUnlinkedAccount()
    {
        var guard = new MemberAccountScopeGuard();
        var linkedOperator = new OperatorBuilder().Build();
        var scope = new MemberAccountScope(LinkedOperator: linkedOperator, AllowedAccountIds: [Guid.NewGuid()]);

        var exception = Should.Throw<TokenWithoutPermissionException>(
            () => guard.EnsureMemberCanActOnAccount(Role.Member, scope, Guid.NewGuid()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE);
    }

    [Fact]
    public void EnsureMemberCanActOnAccount_ShouldNotThrow_WhenLinkedMemberTargetsAllowedAccount()
    {
        var guard = new MemberAccountScopeGuard();
        var accountId = Guid.NewGuid();
        var linkedOperator = new OperatorBuilder().Build();
        var scope = new MemberAccountScope(LinkedOperator: linkedOperator, AllowedAccountIds: [accountId]);

        Should.NotThrow(() => guard.EnsureMemberCanActOnAccount(Role.Member, scope, accountId));
    }
}
