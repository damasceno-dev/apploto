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
            options.AddOperationTransformer<ResponseNumericFidelityOpenApiDocumentTransformer>();
            options.AddDocumentTransformer<ResponseNumericFidelityOpenApiDocumentTransformer>();
            options.AddOperationTransformer<OperationSecurityOpenApiOperationTransformer>();
            options.AddDocumentTransformer((document, _, _) =>
            {
                // M7.6 follow-up Phase 4.3 (Shared M2 H4): two named bearer schemes and per-operation `security`
                // derived from each action's filters; there is no document-wide requirement any more.
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[SecuritySchemeNames.IdentityBearer] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Identity token (global scope) issued by POST /user/login, POST /invitation/register, POST /user/google/sign-in and " +
                                  "POST /user/renew-token, sent as Authorization: Bearer. Used to list and open branches; " +
                                  "a branch token is refused."
                };
                document.Components.SecuritySchemes[SecuritySchemeNames.BranchBearer] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Branch token (branch scope) issued by POST /branch/session for one membership, sent as " +
                                  "Authorization: Bearer. " +
                                  "Role names on a requirement list the branch roles admitted; none listed means any branch role. " +
                                  "An identity token is refused."
                };
                document.Security = null;
                return Task.CompletedTask;
            });
        });
    }
}
