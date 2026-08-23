using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.BranchUsers.Remove;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.BranchUsers.Remove;

public class RemoveBranchUserUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldSoftDeactivateMembership_WhenAdminRemovesMember()
    {
        var branchId = Guid.NewGuid();
        var caller = new BranchUserBuilder()
            .WithBranchId(branchId)
            .WithRole(Role.Admin)
            .Build();
        var target = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithBranchId(branchId)
            .WithRole(Role.Member)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target.Id, target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(target.Id);

        target.Active.ShouldBeFalse();
        response.BranchUser.Active.ShouldBeFalse();
        await branchUsersRepository.Received(1).GetById(target.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldAllowManagerToRemoveManager()
    {
        var branchId = Guid.NewGuid();
        var caller = new BranchUserBuilder()
            .WithBranchId(branchId)
            .WithRole(Role.Manager)
            .Build();
        var target = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithBranchId(branchId)
            .WithRole(Role.Manager)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target.Id, target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(target.Id);

        response.BranchUser.Active.ShouldBeFalse();
        await branchUsersRepository.Received(1).GetById(target.Id);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldAllowManagerToRemoveMember()
    {
        var branchId = Guid.NewGuid();
        var caller = new BranchUserBuilder()
            .WithBranchId(branchId)
            .WithRole(Role.Manager)
            .Build();
        var target = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithBranchId(branchId)
            .WithRole(Role.Member)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target.Id, target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(target.Id);

        target.Active.ShouldBeFalse();
        response.BranchUser.Active.ShouldBeFalse();
        await branchUsersRepository.Received(1).GetById(target.Id);
        await branchUsersRepository.DidNotReceive().CountActiveAdminsByBranchId(Arg.Any<Guid>());
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldAllowAdminToRemoveAdmin_WhenAnotherAdminExists()
    {
        var branchId = Guid.NewGuid();
        var caller = new BranchUserBuilder()
            .WithBranchId(branchId)
            .WithRole(Role.Admin)
            .Build();
        var target = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithBranchId(branchId)
            .WithRole(Role.Admin)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target.Id, target)
            .CountActiveAdminsByBranchId(branchId, 2)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(target.Id);

        response.BranchUser.Active.ShouldBeFalse();
        await branchUsersRepository.Received(1).GetById(target.Id);
        await branchUsersRepository.Received(1).CountActiveAdminsByBranchId(branchId);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenManagerTriesToRemoveAdmin()
    {
        var branchId = Guid.NewGuid();
        var caller = new BranchUserBuilder()
            .WithBranchId(branchId)
            .WithRole(Role.Manager)
            .Build();
        var target = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithBranchId(branchId)
            .WithRole(Role.Admin)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target.Id, target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(target.Id));
        await branchUsersRepository.Received(1).GetById(target.Id);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenRemovalWouldRemoveLastAdmin()
    {
        var branchId = Guid.NewGuid();
        var caller = new BranchUserBuilder()
            .WithBranchId(branchId)
            .WithRole(Role.Admin)
            .Build();
        var target = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithBranchId(branchId)
            .WithRole(Role.Admin)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target.Id, target)
            .CountActiveAdminsByBranchId(branchId, 1)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(target.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.BRANCH_USER_LAST_ADMIN_CONFLICT);
        await branchUsersRepository.Received(1).GetById(target.Id);
        await branchUsersRepository.Received(1).CountActiveAdminsByBranchId(branchId);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenMembershipIsOutsideCurrentBranch()
    {
        var caller = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Admin)
            .Build();
        var target = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Member)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target.Id, target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(target.Id));

        exception.Message.ShouldBe(ResourcesErrorMessages.BRANCH_USER_NOT_FOUND);
        await branchUsersRepository.Received(1).GetById(target.Id);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenBranchUserIdIsEmpty()
    {
        var caller = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Admin)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.BRANCH_USER_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static RemoveBranchUserUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IBranchUsersRepository branchUsersRepository,
        IUnitOfWork unitOfWork)
    {
        return new RemoveBranchUserUseCase(
            authenticationService,
            branchUsersRepository,
            new OperatorsRepositoryBuilder().Build(),
            unitOfWork);
    }
}
