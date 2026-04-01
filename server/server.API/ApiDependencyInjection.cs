using Microsoft.OpenApi.Models;
using server.Domain.Interfaces;
using server.ExceptionHandling;
using server.Token;

namespace server;

public static class ApiDependencyInjection
{
    private const string AuthenticationType = "Bearer";

    private static readonly OpenApiSecurityScheme SecurityScheme = new()
    {
        Type = SecuritySchemeType.Http,
        Description = @"JWT Authorization header using the Bearer scheme.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Scheme = AuthenticationType,
        Reference = new OpenApiReference()
        {
            Id = AuthenticationType,
            Type = ReferenceType.SecurityScheme
        }
    };
    public static void AddApi(this IServiceCollection services)
    {
        services.AddSingleton<IApiExceptionHandler, ApiExceptionHandler>();
        services.AddScoped<ITokenProvider, HttpContextTokenProvider>();
        services.AddHttpContextAccessor();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(AuthenticationType, SecurityScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement { { SecurityScheme, [] } });
        });
    }
}