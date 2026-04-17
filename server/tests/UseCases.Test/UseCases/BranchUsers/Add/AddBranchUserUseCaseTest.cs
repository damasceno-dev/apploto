using CommonTestUtilities.Entities;
using CommonTestUtilities.Repositories;
using CommonTestUtilities.Requests;
using CommonTestUtilities.Services;
using NSubstitute;
using server.Application.UseCases.BranchUsers.Add;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using Shouldly;
using Xunit;

namespace UseCases.Test.UseCases.BranchUsers.Add;

public class AddBranchUserUseCaseTest
{
    [Fact]
    public async Task Execute_ShouldAddMembership_WhenAdminAddsExistingUserById()
    {
        var caller = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Admin)
            .Build();
        var targetUser = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithName("New Member")
            .WithEmail("new.member@example.com")
            .Build();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(targetUser.Id)
            .WithEmail(null)
            .WithRole(Role.Manager)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(targetUser.Id, targetUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetByUserIdAndBranchId(targetUser.Id, caller.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.BranchUser.UserId.ShouldBe(targetUser.Id);
        response.BranchUser.Name.ShouldBe(targetUser.Name);
        response.BranchUser.Role.ShouldBe(Role.Manager);
        await usersRepository.Received(1).GetById(targetUser.Id);
        await branchUsersRepository.Received(1).GetByUserIdAndBranchId(targetUser.Id, caller.BranchId);
        await branchUsersRepository.Received(1).Add(Arg.Is<BranchUser>(branchUser =>
            branchUser.UserId == targetUser.Id &&
            branchUser.BranchId == caller.BranchId &&
            branchUser.Role == Role.Manager &&
            branchUser.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldAllowManagerToAddMemberByEmail()
    {
        var caller = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Manager)
            .Build();
        var targetUser = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithName("Email Lookup")
            .WithEmail("lookup@example.com")
            .Build();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(null)
            .WithEmail(targetUser.Email)
            .WithRole(Role.Member)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetByEmail(targetUser.Email, targetUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetByUserIdAndBranchId(targetUser.Id, caller.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.BranchUser.Email.ShouldBe(targetUser.Email);
        response.BranchUser.Role.ShouldBe(Role.Member);
        await usersRepository.Received(1).GetByEmail(targetUser.Email);
        await branchUsersRepository.Received(1).GetByUserIdAndBranchId(targetUser.Id, caller.BranchId);
        await branchUsersRepository.Received(1).Add(Arg.Any<BranchUser>());
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldReactivateExistingMembership_WhenMembershipIsInactive()
    {
        var caller = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Admin)
            .Build();
        var targetUser = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithName("Reactivated")
            .WithEmail("reactivated@example.com")
            .Build();
        var existingBranchUser = new BranchUserBuilder()
            .WithId(Guid.NewGuid())
            .WithUser(targetUser)
            .WithBranchId(caller.BranchId)
            .WithRole(Role.Member)
            .WithActive(false)
            .Build();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(targetUser.Id)
            .WithEmail(null)
            .WithRole(Role.Manager)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(targetUser.Id, targetUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetByUserIdAndBranchId(targetUser.Id, caller.BranchId, existingBranchUser)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.BranchUser.Id.ShouldBe(existingBranchUser.Id);
        existingBranchUser.Active.ShouldBeTrue();
        existingBranchUser.Role.ShouldBe(Role.Manager);
        await usersRepository.Received(1).GetById(targetUser.Id);
        await branchUsersRepository.Received(1).GetByUserIdAndBranchId(targetUser.Id, caller.BranchId);
        await branchUsersRepository.DidNotReceive().Add(Arg.Any<BranchUser>());
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenTargetUserDoesNotExist()
    {
        var caller = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Admin)
            .Build();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(Guid.NewGuid())
            .WithEmail(null)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(request.UserId!.Value, null)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<NotFoundException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.USER_NOT_FOUND);
        await usersRepository.Received(1).GetById(request.UserId!.Value);
        await branchUsersRepository.DidNotReceive().GetByUserIdAndBranchId(Arg.Any<Guid>(), Arg.Any<Guid>());
        await branchUsersRepository.DidNotReceive().Add(Arg.Any<BranchUser>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenMembershipIsAlreadyActive()
    {
        var caller = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Admin)
            .Build();
        var targetUser = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithEmail("active@example.com")
            .Build();
        var activeBranchUser = new BranchUserBuilder()
            .WithUser(targetUser)
            .WithBranchId(caller.BranchId)
            .WithRole(Role.Member)
            .WithActive(true)
            .Build();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(targetUser.Id)
            .WithEmail(null)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(targetUser.Id, targetUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetByUserIdAndBranchId(targetUser.Id, caller.BranchId, activeBranchUser)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<ConflictException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.BRANCH_USER_ALREADY_ACTIVE);
        await usersRepository.Received(1).GetById(targetUser.Id);
        await branchUsersRepository.Received(1).GetByUserIdAndBranchId(targetUser.Id, caller.BranchId);
        await branchUsersRepository.DidNotReceive().Add(Arg.Any<BranchUser>());
        await unitOfWork.DidNotReceive().Commit();
    }

    [Fact]
    public async Task Execute_ShouldAllowAdminToAddAdmin()
    {
        var caller = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Admin)
            .Build();
        var targetUser = new UserBuilder()
            .WithId(Guid.NewGuid())
            .WithName("Second Admin")
            .WithEmail("second.admin@example.com")
            .Build();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(targetUser.Id)
            .WithEmail(null)
            .WithRole(Role.Admin)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var usersRepository = new UsersRepositoryBuilder()
            .GetById(targetUser.Id, targetUser)
            .Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder()
            .GetByUserIdAndBranchId(targetUser.Id, caller.BranchId, null)
            .Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, unitOfWork);

        var response = await useCase.Execute(request);

        response.BranchUser.Role.ShouldBe(Role.Admin);
        await usersRepository.Received(1).GetById(targetUser.Id);
        await branchUsersRepository.Received(1).GetByUserIdAndBranchId(targetUser.Id, caller.BranchId);
        await branchUsersRepository.Received(1).Add(Arg.Is<BranchUser>(branchUser =>
            branchUser.UserId == targetUser.Id &&
            branchUser.BranchId == caller.BranchId &&
            branchUser.Role == Role.Admin &&
            branchUser.Active));
        await unitOfWork.Received(1).Commit();
    }

    [Fact]
    public async Task Execute_ShouldReject_WhenManagerTriesToAddAdmin()
    {
        var caller = new BranchUserBuilder()
            .WithBranchId(Guid.NewGuid())
            .WithRole(Role.Manager)
            .Build();
        var request = new RequestAddBranchUserJsonBuilder()
            .WithUserId(Guid.NewGuid())
            .WithEmail(null)
            .WithRole(Role.Admin)
            .Build();

        var authenticationService = new AuthenticationServiceBuilder()
            .GetAuthenticatedBranchUser(caller)
            .Build();
        var usersRepository = new UsersRepositoryBuilder().Build();
        var branchUsersRepository = new BranchUsersRepositoryBuilder().Build();
        var unitOfWork = new UnitOfWorkBuilder().Build();

        var useCase = CreateUseCase(authenticationService, usersRepository, branchUsersRepository, unitOfWork);

        var exception = await Should.ThrowAsync<TokenWithoutPermissionException>(() => useCase.Execute(request));

        exception.Message.ShouldBe(ResourcesErrorMessages.TOKEN_WITHOUT_PERMISSION);
        await branchUsersRepository.DidNotReceive().Add(Arg.Any<BranchUser>());
        await unitOfWork.DidNotReceive().Commit();
    }

    private static AddBranchUserUseCase CreateUseCase(
        IAuthenticationService authenticationService,
        IUsersRepository usersRepository,
        IBranchUsersRepository branchUsersRepository,
        IUnitOfWork unitOfWork)
    {
        return new AddBranchUserUseCase(authenticationService, usersRepository, branchUsersRepository, unitOfWork);
    }
}
