using Microsoft.Extensions.DependencyInjection;
using server.Application.Services;
using server.Application.UseCases.Branches.Create;
using server.Application.UseCases.Users.Login;
using server.Application.UseCases.Users.Register;
using server.Application.UseCases.Users.RenewToken;
using server.Domain.Interfaces;

namespace server.Application;

public static class AppDependencyInjection
{
    public static void AddApplication(this IServiceCollection services)
    {
        services.AddScoped<CreateBranchUseCase>();
        services.AddScoped<UserRegisterUseCase>();
        services.AddScoped<UserLoginUseCase>();
        services.AddScoped<UserRenewTokenUseCase>();
        services.AddScoped<PasswordEncryption>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
    }
}
