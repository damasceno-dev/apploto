using CommonTestUtilities.Entities;
using NSubstitute;
using server.Application.Services.Transactions;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.Services.Transactions;

public class TransactionMutationPermissionGuardTest
{
    private static readonly DateTime UtcNow = new(2026, 4, 25, 17, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(Role.Manager)]
    [InlineData(Role.Admin)]
    public void EnsureAllowed_ShouldPass_ForElevatedRolesEvenWhenNotRecordingOperatorAndDifferentDay(Role role)
    {
        var transaction = BuildTransaction(recordedByOperatorId: Guid.NewGuid());
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.IsSameLocalDay(transaction.Date, UtcNow).Returns(false);
        var guard = new TransactionMutationPermissionGuard(branchClock);

        Should.NotThrow(() => guard.EnsureAllowed(transaction, role, callerOperator: null, UtcNow));

        branchClock.DidNotReceiveWithAnyArgs().IsSameLocalDay(default, default);
    }

    [Fact]
    public void EnsureAllowed_ShouldThrowRequiresOperatorLink_WhenMemberHasNoLinkedOperator()
    {
        var transaction = BuildTransaction(recordedByOperatorId: Guid.NewGuid());
        var branchClock = Substitute.For<IBranchClock>();
        var guard = new TransactionMutationPermissionGuard(branchClock);

        var exception = Should.Throw<TokenWithoutPermissionException>(
            () => guard.EnsureAllowed(transaction, Role.Member, callerOperator: null, UtcNow));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_REQUIRES_OPERATOR_LINK);
        branchClock.DidNotReceiveWithAnyArgs().IsSameLocalDay(default, default);
    }

    [Fact]
    public void EnsureAllowed_ShouldThrowNotRecordingOperator_WhenMemberIsLinkedButIsNotRecordingOperator()
    {
        var callerOperator = new OperatorBuilder().Build();
        var transaction = BuildTransaction(recordedByOperatorId: Guid.NewGuid());
        var branchClock = Substitute.For<IBranchClock>();
        var guard = new TransactionMutationPermissionGuard(branchClock);

        var exception = Should.Throw<TokenWithoutPermissionException>(
            () => guard.EnsureAllowed(transaction, Role.Member, callerOperator, UtcNow));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_MEMBER_NOT_RECORDING_OPERATOR);
        branchClock.DidNotReceiveWithAnyArgs().IsSameLocalDay(default, default);
    }

    [Fact]
    public void EnsureAllowed_ShouldThrowRequiresSameDay_WhenMemberIsRecordingOperatorButDateIsDifferentLocalDay()
    {
        var callerOperator = new OperatorBuilder().Build();
        var transaction = BuildTransaction(recordedByOperatorId: callerOperator.Id);
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.IsSameLocalDay(transaction.Date, UtcNow).Returns(false);
        var guard = new TransactionMutationPermissionGuard(branchClock);

        var exception = Should.Throw<TokenWithoutPermissionException>(
            () => guard.EnsureAllowed(transaction, Role.Member, callerOperator, UtcNow));

        exception.Message.ShouldBe(ResourcesErrorMessages.TRANSACTION_UPDATE_REQUIRES_SAME_DAY);
        branchClock.Received(1).IsSameLocalDay(transaction.Date, UtcNow);
    }

    [Fact]
    public void EnsureAllowed_ShouldPass_WhenMemberIsRecordingOperatorAndSameLocalDay()
    {
        var callerOperator = new OperatorBuilder().Build();
        var transaction = BuildTransaction(recordedByOperatorId: callerOperator.Id);
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.IsSameLocalDay(transaction.Date, UtcNow).Returns(true);
        var guard = new TransactionMutationPermissionGuard(branchClock);

        Should.NotThrow(() => guard.EnsureAllowed(transaction, Role.Member, callerOperator, UtcNow));

        branchClock.Received(1).IsSameLocalDay(transaction.Date, UtcNow);
    }

    private static Transaction BuildTransaction(Guid recordedByOperatorId)
    {
        return new TransactionBuilder()
            .WithDate(new DateTime(2026, 4, 25))
            .WithRecordedByOperatorId(recordedByOperatorId)
            .Build();
    }
}
