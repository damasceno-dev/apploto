using Microsoft.Extensions.DependencyInjection;
using server.Application.Services;
using server.Application.UseCases.BranchUsers.Add;
using server.Application.UseCases.BranchUsers.List;
using server.Application.UseCases.BranchUsers.Remove;
using server.Application.UseCases.BranchUsers.UpdateRole;
using server.Application.UseCases.Branches.Create;
using server.Application.UseCases.Branches.CreateSession;
using server.Application.UseCases.Branches.GetCurrentBranchSummary;
using server.Application.UseCases.Branches.ListMyBranches;
using server.Application.UseCases.Operators.Create;
using server.Application.UseCases.Users.Login;
using server.Application.UseCases.Users.Register;
using server.Application.UseCases.Users.RenewToken;
using server.Domain.Interfaces;

namespace server.Application;

public static class AppDependencyInjection
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddScoped<AddBranchUserUseCase>();
        services.AddScoped<ListBranchUsersUseCase>();
        services.AddScoped<UpdateBranchUserRoleUseCase>();
        services.AddScoped<RemoveBranchUserUseCase>();
        services.AddScoped<CreateBranchUseCase>();
        services.AddScoped<ListMyBranchesUseCase>();
        services.AddScoped<CreateBranchSessionUseCase>();
        services.AddScoped<GetCurrentBranchSummaryUseCase>();
        services.AddScoped<CreateOperatorUseCase>();
        services.AddScoped<UserRegisterUseCase>();
        services.AddScoped<UserLoginUseCase>();
        services.AddScoped<UserRenewTokenUseCase>();
        services.AddScoped<PasswordEncryption>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
    }
}
