using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using server.Domain.Interfaces;
using server.ExceptionHandling;
using server.Token;

namespace server;

public static class ApiDependencyInjection
{
    public static void AddApi(this IServiceCollection services)
    {
        services.AddSingleton<IApiExceptionHandler, ApiExceptionHandler>();
        services.AddScoped<ITokenProvider, HttpContextTokenProvider>();
        services.AddHttpContextAccessor();
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "JWT Authorization header using the Bearer scheme."
                };
                document.Security ??= [];
                document.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", document)] = []
                });
                return Task.CompletedTask;
            });
        });
    }
}
