using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.BranchUsers.UpdateRole;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.BranchUsers.UpdateRole;

public class UpdateBranchUserRoleUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldUpdateRole_WhenAdminManagesMember()
    {
        var branchId = Guid.NewGuid();
        var caller = new BranchUserBuilder()
            .WithBranchId(branchId)
            .WithRole(Role.Admin)
            .Build();
        var target = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithBranchId(branchId)
            .WithUser(new UserBuilder().WithName("Target").WithEmail("target@example.com").Build())
            .WithRole(Role.Member)
            .Build();
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(target.Id, request);

        target.Role.ShouldBe(Role.Manager);
        response.BranchUser.Role.ShouldBe(Role.Manager);
        response.BranchUser.Active.ShouldBeTrue();
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldAllowAdminToPromoteMemberToAdmin()
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
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Admin)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(target.Id, request);

        target.Role.ShouldBe(Role.Admin);
        response.BranchUser.Role.ShouldBe(Role.Admin);
        await branchUsersRepository.DidNotReceive().CountActiveAdminsByBranchId(Arg.Any<Guid>());
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldAllowManagerToManageMember()
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
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(target.Id, request);

        response.BranchUser.Role.ShouldBe(Role.Manager);
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenManagerTriesToPromoteToAdmin()
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
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Admin)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(target.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenManagerTriesToManageAdmin()
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
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(target.Id, request));
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenUpdateWouldRemoveLastAdmin()
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
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Member)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target)
            .CountActiveAdminsByBranchId(1)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(target.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.BRANCH_USER_LAST_ADMIN_CONFLICT);
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
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetById(target)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(target.Id, request));

        exception.Message.ShouldBe(ResourcesErrorMessages.BRANCH_USER_NOT_FOUND);
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenBranchUserIdIsEmpty()
    {
        var caller = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Admin)
            .Build();
        var request = new RequestUpdateBranchUserRoleJsonBuilder()
            .WithRole(Role.Manager)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, branchUsersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<OnValidationException>(() => useCase.Execute(Guid.Empty, request));

        exception.GetErrorMessages.ShouldContain(ResourcesErrorMessages.BRANCH_USER_ID_EMPTY);
        await unitOfWork.DidNotReceive().Commit();
    }

    private static UpdateBranchUserRoleUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IBranchUsersRepository branchUsersRepository,
        IUnitOfWork unitOfWork)
    {
        return new UpdateBranchUserRoleUseCase(authenticationService, branchUsersRepository, unitOfWork);
    }
}
