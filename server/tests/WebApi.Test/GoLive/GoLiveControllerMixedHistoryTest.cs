using System.Net;
using CommonTestUtilities.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;
using Xunit;

namespace WebApi.Test.GoLive;

[Collection(ServerApiCollection.Name)]
public class GoLiveControllerMixedHistoryTest(ServerWebApplicationFactory factory)
{
    [Theory]
    [InlineData("DailyCloses")]
    [InlineData("SessionFieldEffects")]
    [InlineData("SettlementEvents")]
    public async Task Historical_count_fee_and_paid_claims_commit_once_or_roll_back_together(string faultTable)
    {
        var client = factory.CreateClient(); var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 mixed history");
        var created = await client.PostAuthAsync("/account/terminal", new RequestCreateTerminalAccountJson { Name = "Synthetic reviewed historical box" }, day.Manager.Token);
        var box = await created.ReadContentAsync<ResponseCreateAccountJson>();
        var request = RequestGoLiveImportJsonBuilder.BuildWindows();
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblEstoque", "1,2026/11/01 0:00:00,6,100.00,1,2026/11/01 0:00:00\n2,2026/11/01 0:00:00,7,5.00,1,2026/11/01 0:00:00\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblLancamentos", "1,2026/11/01 0:00:00,50.00,1,5,Synthetic paid cheque,,2026/11/02 0:00:00,2026/11/02 13:12:00,2,Negativo,1,,,,2026/11/01 0:00:00\n2,2026/11/01 0:00:00,30.00,1,6,Synthetic paid card,,2026/11/02 0:00:00,2026/11/02 14:13:00,2,Negativo,1,,,,2026/11/01 0:00:00\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblConfiguracao", "1,2026/10/31 0:00:00\n");
        var upload = await client.PostAuthAsync("/go-live/imports", request, day.Manager.Token, Guid.NewGuid().ToString());
        upload.StatusCode.ShouldBe(HttpStatusCode.OK, await upload.Content.ReadAsStringAsync());
        var import = await upload.ReadContentAsync<ResponseGoLiveImportJson>();
        var mapping = new RequestGoLiveMappingJson
        {
            ExpectedRevision = 0,
            Resolutions = [
            new() {Table="TblEstoque",SourceId="1",Action="CountedSession",Reason="Synthetic known custody",AccountId=box.Id,TerminalId=box.TerminalId,ProductId=day.Cash.Id,OperatorId=day.MemberA.Operator!.Id,Sequence=1,HistoryComplete=true,StartsAt=new DateTime(2026,11,1,11,0,0,DateTimeKind.Utc),EndsAt=new DateTime(2026,11,1,20,0,0,DateTimeKind.Utc)},
            new() {Table="TblEstoque",SourceId="2",Action="SessionField",Reason="Synthetic legacy fee once",AccountId=box.Id,OperatorId=day.MemberA.Operator!.Id,Sequence=1,Field=SessionCloseField.BolaoFee},
            new() {Table="TblLancamentos",SourceId="1",Action="Transaction",Reason="Synthetic paid cheque evidence",AccountId=day.SafeId,OperatorId=day.MemberA.Operator!.Id,TransactionTypeId=day.SeededTypes["saidas.deposito-cheque"].Id,HistoryComplete=true},
            new() {Table="TblLancamentos",SourceId="2",Action="Transaction",Reason="Synthetic paid card evidence",AccountId=day.SafeId,OperatorId=day.MemberA.Operator!.Id,TransactionTypeId=day.SeededTypes["saidas.cartao-credito"].Id,HistoryComplete=true},
            new() {Table="TblConfiguracao",SourceId="1",Action="LockBoundary",Reason="Exact source lock boundary",SourceDate=new DateTime(2026,10,31),HistoryComplete=true}
        ]
        };
        (await client.PostAuthAsync($"/go-live/imports/{import.Id}/mapping", mapping, day.Manager.Token, Guid.NewGuid().ToString())).StatusCode.ShouldBe(HttpStatusCode.OK);
        var previewResponse = await client.PostAuthAsync($"/go-live/imports/{import.Id}/preview", new RequestGoLiveMappingJson { ExpectedRevision = 1 }, day.Manager.Token, Guid.NewGuid().ToString());
        var preview = await previewResponse.ReadContentAsync<ResponseGoLiveImportJson>();
        preview.Preview!.CanActivate.ShouldBeTrue(System.Text.Json.JsonSerializer.Serialize(preview.Preview.Issues));
        (await day.QueryAsync(db => db.DailyCloses.CountAsync(x => x.AccountId == box.Id))).ShouldBe(0);
        (await day.QueryAsync(db => db.SettlementEvents.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
        var confirm = new RequestActivateGoLiveImportJson { DryRunVersion = preview.Preview.ToolVersion, MappingRevision = 1, PreviewId = preview.PreviewId!.Value, SourceDigest = preview.SourceDigest, TargetFingerprint = preview.TargetFingerprint! };
        var key = Guid.NewGuid().ToString();
        using (factory.Services.GetRequiredService<ArmableCommandInterceptor>().Arm(cmd => cmd.CommandText.Contains($"INSERT INTO \"{faultTable}\"", StringComparison.Ordinal), _ => throw new InvalidOperationException("Synthetic cutover boundary failure")))
            (await client.PostAuthAsync($"/go-live/imports/{import.Id}/activate", confirm, day.Manager.Token, key)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        (await day.QueryAsync(db => db.DailyCloses.CountAsync(x => x.AccountId == box.Id))).ShouldBe(0);
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
        (await day.QueryAsync(db => db.GoLiveImportTargets.CountAsync(x => x.GoLiveImportId == import.Id))).ShouldBe(0);
        var activated = await client.PostAuthAsync($"/go-live/imports/{import.Id}/activate", confirm, day.Manager.Token, key);
        activated.StatusCode.ShouldBe(HttpStatusCode.OK, await activated.Content.ReadAsStringAsync());
        var result = await activated.ReadContentAsync<ResponseGoLiveImportJson>();
        result.TargetIds.Count.ShouldBe(4);
        (await client.PostAuthAsync($"/go-live/imports/{import.Id}/activate", confirm, day.Manager.Token, key)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var events = await day.QueryAsync(db => db.SettlementEvents.AsNoTracking().Where(x => x.BranchId == day.Branch.Id).ToListAsync());
        events.Count.ShouldBe(2); events.ShouldAllBe(x => x.Kind == SettlementEventKind.MigratedSettlement && x.OutstandingDelta == -x.Amount && x.SourcePaidAt != null && x.Provenance == SettlementEventProvenance.MigratedFromPaidAt);
        events.Select(x => x.SettlementInstrument).Order().ShouldBe(new[] { SettlementInstrument.Cheque, SettlementInstrument.CreditCard }.Order());
        (await day.QueryAsync(db => db.BankReceipts.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
        var effect = await day.QueryAsync(db => db.SessionFieldEffects.AsNoTracking().SingleAsync(x => x.BranchId == day.Branch.Id));
        effect.Value.ShouldBe(5); effect.Revision.ShouldBe(1);
        (await day.QueryAsync(db => db.Transactions.AsNoTracking().SingleAsync(x => x.Id == effect.CurrentTransactionId))).Value.ShouldBe(5);
        (await day.QueryAsync(db => db.GoLiveImportTargets.CountAsync(x => x.GoLiveImportId == import.Id))).ShouldBe(5);
        (await day.QueryAsync(db => db.Settings.AsNoTracking().SingleAsync(x => x.BranchId == day.Branch.Id))).LockDate.ShouldBe(new DateTime(2026, 10, 31));
    }
}
