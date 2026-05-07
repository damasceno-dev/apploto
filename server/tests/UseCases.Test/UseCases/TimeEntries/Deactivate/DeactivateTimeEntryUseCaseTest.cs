using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.Services.Transactions;
using server.Application.UseCases.TimeEntries.Deactivate;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.TimeEntries.Deactivate;

public class DeactivateTimeEntryUseCaseTest
{
    private static readonly DateTime FixedNow = new(2026, 5, 7, 14, 0, 0, DateTimeKind.Utc);

    public static TheoryData<string, Role> ElevatedRoles => new()
    {
        { "Manager", Role.Manager },
        { "Admin",   Role.Admin   },
    };

    [Theory]
    [MemberData(nameof(ElevatedRoles))]
    public async Task Execute_ShouldSoftDeleteAndStampAudit_WhenElevatedRoleSubmitsValidId(
        string _,
        Role role)
    {
        var branchUser = new BranchUserBuilder().WithRole(role).Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).WithName("Lenna").Build();
        var timeEntry = new TimeEntryBuilder()
            .WithOperator(op)
            .WithActive(true)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var timeEntriesRepository = new TimeEntriesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(timeEntry.Id, branchUser.BranchId, timeEntry)
            .Build();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedNow);
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = new DeactivateTimeEntryUseCase(
            authenticationService, timeEntriesRepository, branchClock, unitOfWork);

        var response = await useCase.Execute(timeEntry.Id);

        timeEntry.Active.ShouldBeFalse();
        timeEntry.UpdatedAt.ShouldBe(FixedNow);
        timeEntry.UpdatedByUserId.ShouldBe(branchUser.UserId);
        response.Id.ShouldBe(timeEntry.Id);
        response.Active.ShouldBeFalse();
        response.OperatorName.ShouldBe(op.Name);
        response.UpdatedAt.ShouldBe(FixedNow);
        response.UpdatedByUserId.ShouldBe(branchUser.UserId);
        await timeEntriesRepository.Received(1).GetByIdAndBranchId(timeEntry.Id, branchUser.BranchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowTokenWithoutPermission_WhenMemberAttempts()
    {
        var branchUser = new BranchUserBuilder().WithRole(Role.Member).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var timeEntriesRepository = new TimeEntriesRepositoryBuilder().Build();
        var branchClock = Substitute.For<IBranchClock>();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = new DeactivateTimeEntryUseCase(
            authenticationService, timeEntriesRepository, branchClock, unitOfWork);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(
            () => useCase.Execute(Guid.NewGuid()));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await timeEntriesRepository.DidNotReceive().GetByIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowOnValidationException_WhenIdIsEmpty()
    {
        var authenticationService = new AuthenticationServiceBuilder().Build();
        var timeEntriesRepository = new TimeEntriesRepositoryBuilder().Build();
        var branchClock = Substitute.For<IBranchClock>();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = new DeactivateTimeEntryUseCase(
            authenticationService, timeEntriesRepository, branchClock, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(
            () => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.TIMEENTRY_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldThrowNotFoundException_WhenTimeEntryDoesNotExistOrIsAlreadyInactive()
    {
        // Repo's GetByIdAndBranchId filters Active = true; both "missing" and
        // "already inactive" return null and surface as 404.
        var branchUser = new BranchUserBuilder().WithRole(Role.Manager).Build();
        var missingId = Guid.NewGuid();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var timeEntriesRepository = new TimeEntriesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(missingId, branchUser.BranchId, null)
            .Build();
        var branchClock = Substitute.For<IBranchClock>();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = new DeactivateTimeEntryUseCase(
            authenticationService, timeEntriesRepository, branchClock, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => useCase.Execute(missingId));

        exception.Message.ShouldBe(ResourcesErrorMessages.TIMEENTRY_NOT_FOUND);
        await timeEntriesRepository.Received(1).GetByIdAndBranchId(missingId, branchUser.BranchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldUseAuthenticatedBranchScope_WhenLoadingTimeEntry()
    {
        // Branch isolation: the repo call must carry the authenticated branch's id, not
        // an arbitrary one. NSubstitute's exact-arg match on (id, branchId) proves it.
        var branchUser = new BranchUserBuilder().WithRole(Role.Admin).Build();
        var op = new OperatorBuilder().WithBranchId(branchUser.BranchId).Build();
        var timeEntry = new TimeEntryBuilder().WithOperator(op).Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(branchUser)
            .Build();
        var timeEntriesRepository = new TimeEntriesRepositoryBuilder()
            .GetByIdAndBranchIdReturns(timeEntry.Id, branchUser.BranchId, timeEntry)
            .Build();
        var branchClock = Substitute.For<IBranchClock>();
        branchClock.UtcNow().Returns(FixedNow);
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = new DeactivateTimeEntryUseCase(
            authenticationService, timeEntriesRepository, branchClock, unitOfWork);

        await useCase.Execute(timeEntry.Id);

        await timeEntriesRepository.Received(1)
            .GetByIdAndBranchId(timeEntry.Id, branchUser.BranchId);
        await timeEntriesRepository.DidNotReceive()
            .GetByIdAndBranchId(timeEntry.Id, Arg.Is<Guid>(g => g != branchUser.BranchId));
    }
}
