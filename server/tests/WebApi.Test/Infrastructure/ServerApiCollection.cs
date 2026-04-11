using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// xUnit collection that serialises every integration test class through a single
/// <see cref="ServerWebApplicationFactory"/> instance. Keeps the Testcontainers
/// PostgreSQL container to one boot per run and prevents parallel tests from
/// racing on shared rows.
/// </summary>
[CollectionDefinition(Name)]
public class ServerApiCollection : ICollectionFixture<ServerWebApplicationFactory>
{
    public const string Name = "ServerApi";
}
