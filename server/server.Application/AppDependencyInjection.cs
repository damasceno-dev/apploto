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
using server.Application.UseCases.Accounts.Create;
using server.Application.UseCases.Accounts.Deactivate;
using server.Application.UseCases.Accounts.Get;
using server.Application.UseCases.Accounts.List;
using server.Application.UseCases.Accounts.Update;
using server.Application.UseCases.OperatorAccounts.AssignAccount;
using server.Application.UseCases.OperatorAccounts.GetOperatorSelfContext;
using server.Application.UseCases.OperatorAccounts.ListOperatorAccounts;
using server.Application.UseCases.OperatorAccounts.SetPrimaryAccount;
using server.Application.UseCases.OperatorAccounts.UnassignAccount;
using server.Application.UseCases.Operators.Create;
using server.Application.UseCases.Operators.Deactivate;
using server.Application.UseCases.Operators.Get;
using server.Application.UseCases.Operators.List;
using server.Application.UseCases.Operators.Update;
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
        services.AddScoped<CreateAccountUseCase>();
        services.AddScoped<ListAccountsUseCase>();
        services.AddScoped<GetAccountUseCase>();
        services.AddScoped<UpdateAccountUseCase>();
        services.AddScoped<DeactivateAccountUseCase>();
        services.AddScoped<AssignAccountUseCase>();
        services.AddScoped<UnassignAccountUseCase>();
        services.AddScoped<SetPrimaryAccountUseCase>();
        services.AddScoped<ListOperatorAccountsUseCase>();
        services.AddScoped<GetOperatorSelfContextUseCase>();
        services.AddScoped<CreateOperatorUseCase>();
        services.AddScoped<ListOperatorsUseCase>();
        services.AddScoped<GetOperatorUseCase>();
        services.AddScoped<UpdateOperatorUseCase>();
        services.AddScoped<DeactivateOperatorUseCase>();
        services.AddScoped<UserRegisterUseCase>();
        services.AddScoped<UserLoginUseCase>();
        services.AddScoped<UserRenewTokenUseCase>();
        services.AddScoped<PasswordEncryption>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
    }
}
