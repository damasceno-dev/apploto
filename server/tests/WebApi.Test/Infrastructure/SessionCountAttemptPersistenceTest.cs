using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using server.Domain.Entities.Enums;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Sessions;
using Xunit;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// M9.6 Phase 4 database guards for writers that bypass the application: a count attempt and its items are immutable,
/// a session admits one attempt per token and per number, one CountConferred event per attempt, an attempt belongs to a
/// session of its own branch, and the stored calculation is internally consistent.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class SessionCountAttemptPersistenceTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Attempts_ShouldBeImmutableUniqueAndBranchBound()
    {
        var client = factory.CreateClient();
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, client, "AttemptGuards");
        (await SessionCountScenario.SaveCountAsync(client, session.Id, scenario.MemberA.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_000m }))
            .EnsureSuccessStatusCode();
        var attempt = (await client.ConferAsync(session.Id, scenario.MemberA.Token)).Attempt;
        var token = (await factory.ReloadAsync<server.Domain.Entities.SessionCountAttempt>(attempt.Id))!.ConfirmationToken;

        (await Fails($"UPDATE \"SessionCountAttempts\" SET \"Difference\" = 1, \"TotalClosing\" = \"TotalClosing\" + 1 WHERE \"Id\" = '{attempt.Id}'"))
            .ConstraintName.ShouldBe("CK_SessionCountAttempts_Immutable");
        (await Fails($"DELETE FROM \"SessionCountAttempts\" WHERE \"Id\" = '{attempt.Id}'"))
            .ConstraintName.ShouldBe("CK_SessionCountAttempts_Immutable");
        (await Fails($"UPDATE \"SessionCountAttemptItems\" SET \"CountedValue\" = 1 WHERE \"AttemptId\" = '{attempt.Id}'"))
            .ConstraintName.ShouldBe("CK_SessionCountAttempts_Immutable");

        (await Fails(InsertAttempt(scenario.Branch.Id, session.Id, 2, token, scenario.MemberA.User.Id)))
            .ConstraintName.ShouldBe("IX_SessionCountAttempts_SessionId_ConfirmationToken");
        (await Fails(InsertAttempt(scenario.Branch.Id, session.Id, 1, Guid.NewGuid(), scenario.MemberA.User.Id)))
            .ConstraintName.ShouldBe("IX_SessionCountAttempts_SessionId_AttemptNumber");
        (await Fails(InsertAttempt(scenario.Branch.Id, session.Id, 3, Guid.NewGuid(), scenario.MemberA.User.Id, difference: 5m)))
            .ConstraintName.ShouldBe("CK_SessionCountAttempts_DifferenceMatches");

        var otherBranch = await factory.SeedBranchForOtherContextAsync();
        (await Fails(InsertAttempt(otherBranch.Id, session.Id, 4, Guid.NewGuid(), scenario.MemberA.User.Id)))
            .ConstraintName.ShouldBe("FK_SessionCountAttempts_DailyCloses_BranchId_SessionId");

        (await Fails($"""
            INSERT INTO "SessionEvents" ("Id", "BranchId", "SessionId", "Kind", "ActorUserId", "ActorName", "Details", "AttemptId", "CreatedAt", "Active")
            VALUES (gen_random_uuid(), '{scenario.Branch.Id}', '{session.Id}', {(int)SessionEventKind.CountConferred}, '{scenario.MemberA.User.Id}', 'x', '{"{}"}', '{attempt.Id}', now(), true)
            """)).ConstraintName.ShouldBe("IX_SessionEvents_AttemptId_CountConferred");
    }

    private static string InsertAttempt(Guid branchId, Guid sessionId, int number, Guid token, Guid userId, decimal difference = 0m)
    {
        return $"""
            INSERT INTO "SessionCountAttempts" ("Id", "BranchId", "SessionId", "AttemptNumber", "ConfirmationToken", "Kind",
                "CountRevision", "LedgerRevision", "OpeningRevision", "TotalOpening", "TotalIn", "TotalOut", "ExpectedClosing",
                "TotalClosing", "Difference", "ConferredByUserId", "ConferredByName", "CommandKey", "CreatedAt", "Active")
            VALUES (gen_random_uuid(), '{branchId}', '{sessionId}', {number}, '{token}', 0, 0, 0, 0, 10, 0, 0, 10, 10,
                {difference.ToString(System.Globalization.CultureInfo.InvariantCulture)}, '{userId}', 'x', 'k', now(), true)
            """;
    }

    private async Task<PostgresException> Fails(string sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await using var connection = new NpgsqlConnection(scope.ServiceProvider.GetRequiredService<ServerDbContext>().Database.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        return await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
    }
}
