using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Application;
using server.Application.Services.Security;
using server.Application.UseCases.Platform.Bootstrap;
using server.Application.UseCases.Platform.OperateBranch;
using server.Application.UseCases.Platform.ReadOnboarding;
using server.Application.UseCases.Platform.ReissueOwnerInvitation;
using server.Communication.Requests;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using server.Exceptions;
using server.Exceptions.Exceptions;
using server.Infrastructure;

if (args.Length == 0 || args.Contains("--help"))
{
    Console.WriteLine("Uso: loto-cli <bootstrap-platform|onboard|inspect|reissue|set-platform> --environment Development|Staging --connection <PostgreSQL> [opções] [--execute]");
    Console.WriteLine("Em Production, bootstrap-platform exige --connection-env <nome-da-variável> para não expor a credencial no processo.");
    Console.WriteLine("bootstrap-platform: --name <nome> --email <email>; Production exige também --allow-production-bootstrap e --execute.");
    Console.WriteLine("onboard: --actor-user-id <guid> --request-file <json> [--demo-data]; inspect/reissue: --actor-user-id <guid> --request-id <guid>");
    Console.WriteLine("set-platform: --target-user-id <guid> --enabled true|false. Sem --execute, valida e mostra somente o plano.");
    return;
}

try
{
    var command = args[0];
    var values = new Dictionary<string, string>(StringComparer.Ordinal);
    var execute = false;
    var demoData = false;
    var allowProductionBootstrap = false;
    for (var i = 1; i < args.Length; i++)
    {
        if (args[i] == "--execute") { execute = true; continue; }
        if (args[i] == "--demo-data") { demoData = true; continue; }
        if (args[i] == "--allow-production-bootstrap") { allowProductionBootstrap = true; continue; }
        if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("Argumento inválido.");
        if (!values.TryAdd(args[i][2..], args[++i])) throw new ArgumentException("Argumento repetido.");
    }
    string Required(string name) => values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value : throw new ArgumentException("Falta --" + name);
    var environment = Required("environment");
    var productionBootstrap = command == "bootstrap-platform" && environment == "Production" && allowProductionBootstrap && execute;
    if (environment is not ("Development" or "Staging") && !productionBootstrap)
        throw new ArgumentException("Ambiente permitido: Development ou Staging; inicialização Production exige --allow-production-bootstrap e --execute.");
    if (values.ContainsKey("connection") == values.ContainsKey("connection-env"))
        throw new ArgumentException("Informe exatamente um de --connection ou --connection-env.");
    var connectionValue = values.ContainsKey("connection") ? Required("connection") :
        Environment.GetEnvironmentVariable(Required("connection-env")) ?? throw new ArgumentException("Variável de conexão não encontrada.");
    if (productionBootstrap && !values.ContainsKey("connection-env"))
        throw new ArgumentException("Production exige --connection-env.");
    var connection = new NpgsqlConnectionStringBuilder(connectionValue);
    if (string.IsNullOrWhiteSpace(connection.Database) || (!productionBootstrap &&
        (connection.Database.Contains("loto_prod", StringComparison.OrdinalIgnoreCase) ||
        connection.Database.Contains("production", StringComparison.OrdinalIgnoreCase))))
        throw new ArgumentException("Banco de produção recusado.");
    if (allowProductionBootstrap && !productionBootstrap) throw new ArgumentException("--allow-production-bootstrap vale somente para inicialização Production com --execute.");
    var actorId = command is "set-platform" or "bootstrap-platform" ? Guid.Empty : Guid.Parse(Required("actor-user-id"));
    var requestId = command is "inspect" or "reissue" ? Guid.Parse(Required("request-id")) : Guid.Empty;
    var targetId = command == "set-platform" ? Guid.Parse(Required("target-user-id")) : Guid.Empty;
    var enabled = command == "set-platform" ? bool.Parse(Required("enabled")) : false;
    RequestOperateBranchJson? request = null;
    if (command == "onboard")
    {
        request = JsonSerializer.Deserialize<RequestOperateBranchJson>(await File.ReadAllTextAsync(Required("request-file")),
            new JsonSerializerOptions(JsonSerializerDefaults.Web)) ?? throw new ArgumentException("JSON inválido.");
        if (request.RequestId == Guid.Empty) throw new ArgumentException("RequestId é obrigatório.");
        if (demoData)
        {
            if (!request.OwnerEmail.EndsWith("@example.invalid", StringComparison.OrdinalIgnoreCase) ||
                request.Terminals.Count != 0 || request.AdditionalProducts.Count != 0 || request.Operators.Count != 0)
                throw new ArgumentException("Dados de demonstração exigem e-mail @example.invalid e configuração opcional vazia.");
            request = new RequestOperateBranchJson
            {
                RequestId = request.RequestId, Name = request.Name, Cnpj = request.Cnpj,
                Address = request.Address, Phone = request.Phone, OwnerEmail = request.OwnerEmail,
                Terminals = [new RequestOnboardingTerminalJson { Name = "Terminal demonstração", PairedTabName = "Fiado demonstração" }],
                AdditionalProducts = ["Produto demonstração"],
                Operators = [new RequestOnboardingOperatorJson { Name = "Operador demonstração", TerminalIndexes = [0] }]
            };
        }
    }
    else if (demoData) throw new ArgumentException("--demo-data vale somente para onboard.");
    if (command is not ("bootstrap-platform" or "onboard" or "inspect" or "reissue" or "set-platform")) throw new ArgumentException("Comando desconhecido.");
    var bootstrapName = command == "bootstrap-platform" ? Required("name") : string.Empty;
    var bootstrapEmail = command == "bootstrap-platform" ? Required("email") : string.Empty;
    Console.WriteLine($"Ambiente={environment}; banco={connection.Database}; comando={command}; executar={execute}; demonstração={demoData}");

    var config = new ConfigurationBuilder().AddEnvironmentVariables(prefix: "LOTO_CLI_")
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString,
            // The CLI never creates application JWTs; satisfy the shared infrastructure constructor with an ephemeral key.
            ["Token:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            ["Token:ExpirationTimeInMinutes"] = "15"
        }).Build();
    var services = new ServiceCollection();
    services.AddLogging();
    services.AddApplication();
    services.AddInfrastructure(config);
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton(AccountSecurityOptionsFactory.Create(
        config["AccountSecurity:ApplicationUrl"], config["AccountSecurity:ResetMinutes"],
        config["AccountSecurity:InvitationHours"], config["AccountSecurity:RecoveryCooldownSeconds"]));
    services.AddScoped<IAuthenticationService>(_ => new CliAuthenticationService(actorId, _.GetRequiredService<IUsersRepository>()));
    await using var provider = services.BuildServiceProvider();
    await using var scope = provider.CreateAsyncScope();
    if (command == "bootstrap-platform")
    {
        if (productionBootstrap && (!bool.TryParse(config["SecurityMail:Enabled"], out var mailEnabled) || !mailEnabled ||
            bool.TryParse(config["SecurityMail:LocalCapture"], out var localCapture) && localCapture ||
            scope.ServiceProvider.GetRequiredService<AccountSecurityOptions>().ApplicationUrl.Scheme != "https"))
            throw new ArgumentException("Production exige SMTP habilitado com TLS e URL HTTPS confiável.");
        var id = await scope.ServiceProvider.GetRequiredService<BootstrapPlatformOperatorUseCase>()
            .Execute(bootstrapName, bootstrapEmail, execute);
        Console.WriteLine($"UserId={id}; ativação={(execute ? "enviada" : "pendente de --execute")}");
    }
    else if (command == "onboard")
    {
        var validation = new OperateBranchFluentValidation().Validate(request!);
        if (!validation.IsValid)
            throw new OnValidationException(validation.Errors.Select(x => x.ErrorMessage).ToList());
        var actor = await scope.ServiceProvider.GetRequiredService<IAccountSecurityRepository>().GetUserForUpdate(actorId);
        if (actor is null || !actor.IsPlatformOperator)
            throw new TokenWithoutPermissionException(ResourcesErrorMessages.PLATFORM_ONLY);
        if (!execute) return;
        var result = await scope.ServiceProvider.GetRequiredService<OperateBranchUseCase>().Execute(request!);
        Console.WriteLine($"BranchId={result.Response.BranchId}; RequestId={result.Response.RequestId}; criado={result.Created}; entrega={result.Response.DeliveryStatus}");
    }
    else if (command == "inspect")
    {
        var result = await scope.ServiceProvider.GetRequiredService<ReadOperatedOnboardingUseCase>().Execute(requestId);
        Console.WriteLine($"BranchId={result.BranchId}; RequestId={result.RequestId}; convite={result.OwnerInvitationStatus}; entrega={result.DeliveryStatus}");
    }
    else if (command == "reissue")
    {
        if (!execute) { await scope.ServiceProvider.GetRequiredService<ReadOperatedOnboardingUseCase>().Execute(requestId); return; }
        var result = await scope.ServiceProvider.GetRequiredService<ReissueOwnerInvitationUseCase>().Execute(requestId);
        Console.WriteLine($"BranchId={result.BranchId}; convite={result.OwnerInvitationStatus}; entrega={result.DeliveryStatus}");
    }
    else
    {
        var security = scope.ServiceProvider.GetRequiredService<IAccountSecurityRepository>();
        var coordination = scope.ServiceProvider.GetRequiredService<ISecurityCoordination>();
        var user = await security.GetUserForUpdate(targetId) ?? throw new ArgumentException("Usuário ativo não encontrado.");
        await using var lockScope = await SecurityRules.Acquire(coordination, SecurityRules.EmailKey(user.Email));
        user = await security.GetUserForUpdate(targetId) ?? throw new ArgumentException("Usuário ativo não encontrado.");
        if (!execute) { await lockScope.Complete(); return; }
        user.IsPlatformOperator = enabled;
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().Commit();
        await lockScope.Complete();
        Console.WriteLine($"UserId={user.Id}; operador-plataforma={user.IsPlatformOperator}");
    }
}
catch (Exception error)
{
    Console.Error.WriteLine("Falha: " + (error is ServerException serverError ? string.Join("; ", serverError.GetErrorMessages)
        : error is ArgumentException or InvalidOperationException ? error.Message : "verifique o ambiente e os dados"));
    Environment.ExitCode = 1;
}

internal sealed class CliAuthenticationService(Guid actorId, IUsersRepository users) : IAuthenticationService
{
    public async Task<User> GetAuthenticatedUser() => await users.GetById(actorId)
        ?? throw new TokenWithoutUserException(ResourcesErrorMessages.TOKEN_WITHOUT_USER);
    public Task<BranchUser> GetAuthenticatedBranchUser() => throw new NotSupportedException();
    public Task<BranchUser> GetAuthorizedBranchUser(Role requiredRole, params Role[] additionalRoles) => throw new NotSupportedException();
}
