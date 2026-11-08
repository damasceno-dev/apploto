using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;
namespace WebApi.Test.People;

[Collection(ServerApiCollection.Name)]
public class PeopleControllerConcurrencyTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public async Task AuthorityRevokedWhileCommandWaits_RefusesWriteBeforeIdempotencyReservation()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s = await PeopleTestScenario.Seed(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var security = scope.ServiceProvider.GetRequiredService<ISecurityCoordination>();
        await using var held = await security.TryAcquire(server.Application.Services.Security.SecurityRules.BranchKey(s.BranchId));
        held.ShouldNotBeNull();
        var request = client.PostAuthAsync("/people/pay-category", PeopleRequestBuilder.Category(), s.Token, Guid.NewGuid().ToString("N"));
        var db = scope.ServiceProvider.GetRequiredService<server.Infrastructure.ServerDbContext>();
        var membership = await db.BranchUsers.SingleAsync(r => r.Id == s.MembershipId);
        membership.Role = Role.Member;
        await db.SaveChangesAsync();
        await held.Complete();
        (await request).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await factory.ReadDb(async stored =>
        {
            (await stored.EmployeePayCategories.CountAsync(r => r.BranchId == s.BranchId)).ShouldBe(0);
            (await stored.IdempotencyRequests.CountAsync(r => r.BranchId == s.BranchId)).ShouldBe(0);
            return 0;
        });
    }

    [Fact]
    public async Task Pay_ConcurrentExactReplay_PayloadConflict_AppendCorrectionAndCancel_AndLostAuthority()
    {
        using var clock = factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s=await PeopleTestScenario.Seed(factory);
        var category=await s.Create<ResponsePayCategoryJson>(client,"pay-category",PeopleRequestBuilder.Category());
        var pay=PeopleRequestBuilder.Pay(s.Person.Id,category.Id,100);
        var key=Guid.NewGuid().ToString("N");
        var replies=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>client.PostAuthAsync("/people/pay-item",pay,s.Token,key)));
        foreach(var r in replies) r.StatusCode.ShouldBe(HttpStatusCode.Created,await r.Content.ReadAsStringAsync());
        var first=await replies[0].ReadContentAsync<ResponsePayItemJson>();
        (await replies[1].ReadContentAsync<ResponsePayItemJson>()).Id.ShouldBe(first.Id);
        await factory.ReadDb(async db=>{(await db.EmployeePayItems.CountAsync(r=>r.BranchId==s.BranchId)).ShouldBe(1);return 0;});
        pay.Amount=101;
        (await client.PostAuthAsync("/people/pay-item",pay,s.Token,key)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        pay.Amount=125;pay.PreviousId=first.Id;pay.PostedOn=new DateTime(2026,9,2);
        var mutations=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>client.PostAuthAsync($"/people/pay-item/{first.Id}/revision",pay,s.Token,Guid.NewGuid().ToString("N"),first.Version)));
        mutations.Count(r=>r.StatusCode==HttpStatusCode.Created).ShouldBe(1);mutations.Count(r=>r.StatusCode==HttpStatusCode.Conflict).ShouldBe(1);
        var successor=await mutations.Single(r=>r.StatusCode==HttpStatusCode.Created).ReadContentAsync<ResponsePayItemJson>();
        successor.SignedContribution.ShouldBe(25);
        // The same key is independent on the actual revision endpoint, while exact retry of that endpoint replays.
        var endpointKey = Guid.NewGuid().ToString("N");
        var another = PeopleRequestBuilder.Pay(s.Person.Id, category.Id, 10);
        var independent = await s.Create<ResponsePayItemJson>(client, "pay-item", another, endpointKey);
        another.PreviousId = independent.Id; another.Amount = 20;
        var revisionEndpoint = $"/people/pay-item/{independent.Id}/revision";
        var independentRevision = await client.PostAuthAsync(revisionEndpoint, another, s.Token, endpointKey, independent.Version);
        independentRevision.StatusCode.ShouldBe(HttpStatusCode.Created, await independentRevision.Content.ReadAsStringAsync());
        var repeatedRevision = await client.PostAuthAsync(revisionEndpoint, another, s.Token, endpointKey, independent.Version);
        (await repeatedRevision.ReadContentAsync<ResponsePayItemJson>()).Id.ShouldBe((await independentRevision.ReadContentAsync<ResponsePayItemJson>()).Id);

        // Locked original remains immutable; cancellation offsets in October, not a closed-month edit.
        await factory.ReadDb(async db=>{var setting=await db.Settings.SingleAsync(r=>r.BranchId==s.BranchId);setting.LockDate=new DateTime(2026,9,30);await db.SaveChangesAsync();return 0;});
        pay.PreviousId=successor.Id;pay.Cancelled=true;pay.PostedOn=new DateTime(2026,10,1);
        var cancel=await client.PostAuthAsync($"/people/pay-item/{successor.Id}/revision",pay,s.Token,Guid.NewGuid().ToString("N"),successor.Version);
        cancel.StatusCode.ShouldBe(HttpStatusCode.Created,await cancel.Content.ReadAsStringAsync());
        (await cancel.ReadContentAsync<ResponsePayItemJson>()).SignedContribution.ShouldBe(-125);
        (await s.Statement(client,extra:"&asOfDate=2026-09-30")).RecordedPaySignedTotal.ShouldBe(145);
        (await s.Statement(client,10)).RecordedPaySignedTotal.ShouldBe(-125);
        await factory.ReadDb(async db=>{var member=await db.BranchUsers.SingleAsync(r=>r.Id==s.MembershipId);member.Role=Role.Member;await db.SaveChangesAsync();return 0;});
        pay.Amount=100;pay.PreviousId=null;pay.Cancelled=false;pay.PostedOn=PeopleRequestBuilder.September;
        (await client.PostAuthAsync("/people/pay-item",pay,s.Token,key)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Rules_ConcurrentOverlap_AndMonthLockBoundary_AndSameDayAmendment()
    {
        using var clock=factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s=await PeopleTestScenario.Seed(factory);var request=PeopleRequestBuilder.Rule(s.Person.Id);
        var replies=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>client.PostAuthAsync("/people/commission-rule",request,s.Token,Guid.NewGuid().ToString("N"))));
        replies.Count(r=>r.StatusCode==HttpStatusCode.Created).ShouldBe(1);replies.Count(r=>r.StatusCode==HttpStatusCode.Conflict).ShouldBe(1);
        var rule=await replies.Single(r=>r.StatusCode==HttpStatusCode.Created).ReadContentAsync<ResponseCommissionRuleJson>();
        request.PreviousId=rule.Id;request.RatePercent=20;
        var revised=await client.PostAuthAsync($"/people/commission-rule/{rule.Id}/revision",request,s.Token,Guid.NewGuid().ToString("N"),rule.Version);
        revised.StatusCode.ShouldBe(HttpStatusCode.Created,await revised.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<EmployeeCommissionRule>(rule.Id))!.RatePercent.ShouldBe(18.5m);
        await using(var scope=factory.Services.CreateAsyncScope())
        {
            var boundary=scope.ServiceProvider.GetRequiredService<IMonthLockCoordination>();
            await using var held=await boundary.TryAcquireExclusive(s.BranchId);held.ShouldNotBeNull();
            var blocked=client.PostAuthAsync("/people/hour-bank-entry",PeopleRequestBuilder.Hours(s.Person.Id),s.Token,Guid.NewGuid().ToString("N"));
            await using(var update=factory.Services.CreateAsyncScope())
            {
                var db=update.ServiceProvider.GetRequiredService<server.Infrastructure.ServerDbContext>();
                var setting=await db.Settings.SingleAsync(r=>r.BranchId==s.BranchId);setting.LockDate=new DateTime(2026,9,30);await db.SaveChangesAsync();
            }
            await held.Complete();
            var response=await blocked;response.StatusCode.ShouldBe(HttpStatusCode.Conflict,await response.Content.ReadAsStringAsync());
        }
        await factory.ReadDb(async db=>{(await db.HourBankEntries.CountAsync(r=>r.BranchId==s.BranchId)).ShouldBe(0);return 0;});
    }

    [Fact]
    public async Task BranchAndSourceAuthority_AreEnforcedBeforeReplay_AndDeductionRequiresExplicitPolicy()
    {
        using var clock=factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var a=await PeopleTestScenario.Seed(factory);var b=await PeopleTestScenario.Seed(factory);
        var category=await a.Create<ResponsePayCategoryJson>(client,"pay-category",PeopleRequestBuilder.Category(PayItemKind.Deduction));
        var request=PeopleRequestBuilder.Pay(a.Person.Id,category.Id,40,PayItemKind.Deduction);request.DeductionPolicy=null;
        (await client.PostAuthAsync("/people/pay-item",request,a.Token,Guid.NewGuid().ToString("N"))).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        request.DeductionPolicy="Política sintética explícita";
        request.OperatorId=b.Person.Id;
        (await client.PostAuthAsync("/people/pay-item",request,a.Token,Guid.NewGuid().ToString("N"))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var read=await client.GetAuthAsync($"/people/pay-category/{category.Id}",b.Token);read.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var (_,_,_,member)=await factory.SeedFullBranchContextAsync("M98Member",Role.Member);
        (await client.GetAuthAsync($"/report/employee-statement?operatorId={a.Person.Id}&year=2026&month=9",member)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AuditFlushFailure_RollsBackRecordAndStoredSuccess_ThenRetryCanSucceed()
    {
        using var clock=factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s=await PeopleTestScenario.Seed(factory);
        var request=PeopleRequestBuilder.Category();var key=Guid.NewGuid().ToString("N");
        // Fail the success-envelope write after the business row was flushed, proving transaction rollback.
        var trigger="m98_fail_"+Guid.NewGuid().ToString("N");
        await factory.ReadDb(async db=>
        {
            #pragma warning disable EF1002
            return await db.Database.ExecuteSqlRawAsync($"""
                CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN
                IF NEW."BranchId"='{s.BranchId}'::uuid AND NEW."ResponseEnvelope"<>'' THEN RAISE EXCEPTION 'M98_TEST_AUDIT_FLUSH'; END IF; RETURN NEW; END $$;
                CREATE TRIGGER {trigger} BEFORE INSERT OR UPDATE ON "IdempotencyRequests" FOR EACH ROW EXECUTE FUNCTION {trigger}();
                """);
            #pragma warning restore EF1002
        });
        try
        {
            (await client.PostAuthAsync("/people/pay-category",request,s.Token,key)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            await factory.ReadDb(async db=>{(await db.EmployeePayCategories.CountAsync(r=>r.BranchId==s.BranchId)).ShouldBe(0);(await db.IdempotencyRequests.CountAsync(r=>r.BranchId==s.BranchId)).ShouldBe(0);return 0;});
        }
        finally
        {
            await factory.ReadDb(async db=>
            {
                #pragma warning disable EF1002
                return await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER {trigger} ON \"IdempotencyRequests\"; DROP FUNCTION {trigger}();");
                #pragma warning restore EF1002
            });
        }
        await s.Create<ResponsePayCategoryJson>(client,"pay-category",request,key);
    }

    [Fact]
    public async Task Database_RejectsDirectMutation_CrossBranchAndOverlappingSqlInsert()
    {
        using var clock=factory.FreezeBranchLocalTime(new DateTime(2026,11,6,10,0,0));
        var s=await PeopleTestScenario.Seed(factory);
        var category=await s.Create<ResponsePayCategoryJson>(client,"pay-category",PeopleRequestBuilder.Category());
        var exception=await Should.ThrowAsync<PostgresException>(()=>factory.ReadDb(db=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"EmployeePayCategories\" SET \"Name\"='mutated' WHERE \"Id\"={category.Id}")));
        exception.ConstraintName.ShouldBe("CK_People_Immutable");
        var request=PeopleRequestBuilder.Rule(s.Person.Id);
        await s.Create<ResponseCommissionRuleJson>(client,"commission-rule",request);
        var attempts=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>
        {
            try
            {
                await factory.ReadDb(async db=>{db.EmployeeCommissionRules.Add(new EmployeeCommissionRule{BranchId=s.BranchId,OperatorId=s.Person.Id,ActorUserId=s.UserId,PostedOn=request.PostedOn,EffectiveFrom=request.EffectiveFrom,EffectiveTo=request.EffectiveTo,RatePercent=20,IncludeBolaoFee=true,Reason="SQL race",Evidence="Synthetic explicit"});return await db.SaveChangesAsync();});
                return "unexpected";
            }
            catch(DbUpdateException ex) when(ex.InnerException is PostgresException pg){return pg.ConstraintName;}
        }));
        attempts.ShouldAllBe(c=>c=="CK_People_RuleOverlap");
    }
}
