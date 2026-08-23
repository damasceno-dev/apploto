using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Domain.Entities;
using server.Infrastructure;
using Shouldly;
using Xunit;

namespace WebApi.Test.Infrastructure;

[Collection(ServerApiCollection.Name)]
public sealed class PostgresConcurrencyPersistenceTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public void Model_ShouldMapAggregateVersionsToPostgresXminConcurrencyTokens()
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ServerDbContext>();

        AssertXmin<DailyClose>(dbContext, nameof(DailyClose.Version));
        AssertXmin<Transaction>(dbContext, nameof(Transaction.Version));
        AssertXmin<Setting>(dbContext, nameof(Setting.Version));
    }

    private static void AssertXmin<TEntity>(ServerDbContext dbContext, string propertyName)
        where TEntity : class
    {
        var property = dbContext.Model.FindEntityType(typeof(TEntity))?.FindProperty(propertyName);
        property.ShouldNotBeNull();
        property.IsConcurrencyToken.ShouldBeTrue();
        property.GetColumnName().ShouldBe("xmin");
    }
}
