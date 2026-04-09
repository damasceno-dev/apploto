using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;

namespace server.Infrastructure;

public class ServerDbContextFactory : IDesignTimeDbContextFactory<ServerDbContext>
{
    public ServerDbContext CreateDbContext(string[] args)
    {
        var connectionString = ResolveConnectionString();
        var optionsBuilder = new DbContextOptionsBuilder<ServerDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ServerDbContext(optionsBuilder.Options);
    }

    private static string ResolveConnectionString()
    {
        var environmentConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(environmentConnectionString) is false)
        {
            return environmentConnectionString;
        }

        var apiProjectPath = ResolveApiProjectPath();
        var environmentName =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ??
            "Development";

        var baseConnectionString = TryReadConnectionString(Path.Combine(apiProjectPath, "appsettings.json"));
        var environmentConnection = TryReadConnectionString(
            Path.Combine(apiProjectPath, $"appsettings.{environmentName}.json"));

        return environmentConnection ??
               baseConnectionString ??
               throw new InvalidOperationException(
                   $"Could not resolve ConnectionStrings:DefaultConnection from {apiProjectPath}.");
    }

    private static string ResolveApiProjectPath()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "server.API"),
            Path.Combine(Directory.GetCurrentDirectory(), "../server.API"),
            Path.Combine(AppContext.BaseDirectory, "../../../../server.API"),
            Path.Combine(AppContext.BaseDirectory, "../../../../../server.API")
        };

        var apiProjectPath = candidates
            .Select(Path.GetFullPath)
            .FirstOrDefault(Directory.Exists);

        return apiProjectPath ??
               throw new InvalidOperationException("Could not locate the server.API directory for design-time configuration.");
    }

    private static string? TryReadConnectionString(string path)
    {
        if (File.Exists(path) is false)
        {
            return null;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        if (document.RootElement.TryGetProperty("ConnectionStrings", out var connectionStrings) is false)
        {
            return null;
        }

        return connectionStrings.TryGetProperty("DefaultConnection", out var defaultConnection)
            ? defaultConnection.GetString()
            : null;
    }
}
