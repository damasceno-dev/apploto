using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using server.Communication.Responses;
using server.Domain.Interfaces;
using server.ExceptionHandling;
using server.Exceptions;
using server.OpenApi;
using server.Serialization;
using server.Token;

namespace server;

public static class ApiDependencyInjection
{
    public static void AddApi(this IServiceCollection services)
    {
        services.AddSingleton<IApiExceptionHandler, ApiExceptionHandler>();
        services.AddScoped<ITokenProvider, HttpContextTokenProvider>();
        services.AddHttpContextAccessor();
        services.Configure<JsonOptions>(options =>
        {
            options.AllowInputFormatterExceptionMessages = false;
            options.JsonSerializerOptions.Converters.Add(new DeclaredNameEnumJsonConverterFactory());
        });
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
                new ResponseErrorJson(ContractModelStateMessages.Describe(context.ModelState)));
        });
        services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer<NamedEnumOpenApiSchemaTransformer>();
            options.AddSchemaTransformer<CommunicationRequiredPropertiesOpenApiSchemaTransformer>();
            options.AddOperationTransformer<NamedEnumOpenApiOperationTransformer>();
            options.AddOperationTransformer<RequiredQueryParametersOpenApiOperationTransformer>();
            options.AddOperationTransformer<FinancialHeadersOpenApiOperationTransformer>();
            options.AddDocumentTransformer((document, _, _) =>
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
