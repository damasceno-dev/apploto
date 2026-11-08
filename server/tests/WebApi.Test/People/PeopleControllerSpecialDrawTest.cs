using System.Net;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;
using Xunit;
namespace WebApi.Test.People;

[Collection(ServerApiCollection.Name)]
public class PeopleControllerSpecialDrawTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient client=factory.CreateClient();
    [Fact]
    public async Task PostedOfficialSpecialFee_ExplicitAllocation_UnknownRemainder_RetirementAndCancellation_DoNotChangeFinancialSource()
    {
        var today=SessionFlow.LocalToday();
        using var clock=factory.FreezeBranchLocalTime(today.AddHours(10));
        var s=await FinancialDayScenario.CreateAsync(factory,client,"M98Draw");
        var session=await s.StartAsync();
        await s.SaveOkAsync(session,1050,fields:FinancialDayScenario.Fields(bolao:35,special:15));
        await client.ConferAsync(session,s.MemberA.Token);
        await client.SubmitAsync(session,s.MemberA.Token);
        var effect=await factory.ReadDb(db=>db.SessionFieldEffects.AsNoTracking().SingleAsync(e=>e.SessionId==session && e.Field==SessionCloseField.SpecialFee));
        var financialBefore=await s.RowsAsync();
        var drawRequest=new RequestSpecialDrawJson{Name="Sorteio sintético <sem inferência>",Modality="Modalidade explícita",Contest="Concurso 123",PeriodFrom=today,PeriodTo=today,Prize=1000000,PostedOn=today,Reason="Catálogo de demonstração",Evidence="Fixture sintética"};
        async Task<T> Create<T>(string route,object request) where T:class
        {
            var response=await client.PostAuthAsync("/people/"+route,request,s.Manager.Token,Guid.NewGuid().ToString("N"));
            response.StatusCode.ShouldBe(HttpStatusCode.Created,await response.Content.ReadAsStringAsync());return await response.ReadContentAsync<T>();
        }
        var draw=await Create<ResponseSpecialDrawJson>("special-draw",drawRequest);
        var request=new RequestSpecialDrawAllocationJson{DrawId=draw.Id,SessionId=session,SessionFieldEffectId=effect.Id,SourceRevision=effect.Revision,SourceDate=today,Amount=8,PostedOn=today,Reason="Distribuição documental",Evidence="Documento sintético de venda"};
        var allocation=await Create<ResponseSpecialDrawAllocationJson>("special-draw-allocation",request);
        // A second independent allocation cannot force all fees onto a draw.
        (await client.PostAuthAsync("/people/special-draw-allocation",request,s.Manager.Token,Guid.NewGuid().ToString("N"))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var url=$"/report/employee-statement?operatorId={s.MemberA.Operator!.Id}&year={today.Year}&month={today.Month}";
        var report=await (await client.GetAuthAsync(url,s.Manager.Token)).ReadContentAsync<ResponseEmployeeStatementJson>();
        report.SpecialDraws.SourceFeeTotal.ShouldBe(15);report.SpecialDraws.AllocatedFeeTotal.ShouldBe(8);report.SpecialDraws.UnallocatedFeeTotal.ShouldBe(7);
        report.SpecialDraws.Draws.Single().DrawId.ShouldBe(draw.Id);report.SpecialDraws.SourceMappingComplete.ShouldBeTrue();
        clock.AdvanceTo(today.AddDays(1).AddHours(10));
        drawRequest.PreviousId=draw.Id;drawRequest.Enabled=false;drawRequest.PostedOn=today.AddDays(1);
        var retirement=await client.PostAuthAsync($"/people/special-draw/{draw.Id}/revision",drawRequest,s.Manager.Token,Guid.NewGuid().ToString("N"),draw.Version);
        retirement.StatusCode.ShouldBe(HttpStatusCode.Created,await retirement.Content.ReadAsStringAsync());
        request.PreviousId=allocation.Id;request.Cancelled=true;request.PostedOn=today.AddDays(1);
        var cancel=await client.PostAuthAsync($"/people/special-draw-allocation/{allocation.Id}/revision",request,s.Manager.Token,Guid.NewGuid().ToString("N"),allocation.Version);
        cancel.StatusCode.ShouldBe(HttpStatusCode.Created,await cancel.Content.ReadAsStringAsync());
        var prior=await (await client.GetAuthAsync(url+$"&asOfDate={today:yyyy-MM-dd}",s.Manager.Token)).ReadContentAsync<ResponseEmployeeStatementJson>();
        prior.SpecialDraws.AllocatedFeeTotal.ShouldBe(8);prior.SpecialDraws.Draws.Single().Name.ShouldBe(drawRequest.Name);
        var current=await (await client.GetAuthAsync(url,s.Manager.Token)).ReadContentAsync<ResponseEmployeeStatementJson>();
        current.SpecialDraws.AllocatedFeeTotal.ShouldBe(0);current.SpecialDraws.UnallocatedFeeTotal.ShouldBe(15);
        (await factory.ReloadAsync<SessionFieldEffect>(effect.Id))!.Value.ShouldBe(15);
        var financialAfter=await s.RowsAsync();financialAfter.Select(r=>(r.Id,r.Value,r.Status)).ShouldBe(financialBefore.Select(r=>(r.Id,r.Value,r.Status)));
    }
}
