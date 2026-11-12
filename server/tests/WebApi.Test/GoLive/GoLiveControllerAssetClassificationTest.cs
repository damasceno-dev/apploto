using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;
using Xunit;

namespace WebApi.Test.GoLive;

[Collection(ServerApiCollection.Name)]
public class GoLiveControllerAssetClassificationTest(ServerWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task An_opening_cannot_recognize_operating_income_or_expense(bool cheque)
    {
        var client=factory.CreateClient();var day=await FinancialDayScenario.CreateAsync(factory,client,"M10 opening asset class");
        var request=Opening(day,cheque);
        await day.QueryAsync(db=>db.TransactionTypes.Where(x=>x.Id==request.TransactionTypeId).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.FinancialClass,FinancialClass.OperatingExpenseAndLoss)));
        var response=await client.PostAuthAsync("/go-live/openings",request,day.Manager.Token,Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await day.QueryAsync(db=>db.Transactions.CountAsync(x=>x.BranchId==day.Branch.Id))).ShouldBe(0);
        (await day.QueryAsync(db=>db.GoLiveOpenings.CountAsync(x=>x.BranchId==day.Branch.Id))).ShouldBe(0);
    }

    [Fact]
    public async Task Type_changes_cannot_reclassify_an_opening_asset_and_the_database_guards_new_rows()
    {
        var client=factory.CreateClient();var day=await FinancialDayScenario.CreateAsync(factory,client,"M10 protected asset");
        var request=Opening(day,false);
        var response=await client.PostAuthAsync("/go-live/openings",request,day.Manager.Token,Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var opening=await response.ReadContentAsync<ResponseGoLiveOpeningJson>();var id=opening.TransactionIds.Single();
        var original=await day.QueryAsync(db=>db.Transactions.AsNoTracking().SingleAsync(x=>x.Id==id));
        await day.QueryAsync(db=>db.TransactionTypes.Where(x=>x.Id==request.TransactionTypeId).ExecuteUpdateAsync(x=>x.SetProperty(t=>t.FinancialClass,FinancialClass.OperatingExpenseAndLoss)));
        var candidates=await client.GetAuthAsync($"/transaction-type/{request.TransactionTypeId}/reclassification-candidates?DateFrom={day.Today:yyyy-MM-dd}&DateTo={day.Today:yyyy-MM-dd}",day.Manager.Token);
        candidates.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await candidates.ReadContentAsync<ResponseReclassificationJson>()).Rows.ShouldBeEmpty();
        var reclassify=await client.PostAuthAsync($"/transaction-type/{request.TransactionTypeId}/reclassify",new RequestReclassifyTransactionTypeJson {DateFrom=day.Today,DateTo=day.Today,Reason="Opening asset must stay outside own result",Rows=[new(){TransactionId=id,Version=original.Version}]},day.Manager.Token);
        reclassify.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await day.QueryAsync(db=>db.Transactions.AsNoTracking().SingleAsync(x=>x.Id==id))).PostedFinancialClass.ShouldBe(FinancialClass.CaixaAndClientMoney);
        (await client.FinancialResultAsync(day.Manager.Token,day.Today)).KnownOwnResult.ShouldBe(0m);
        var invalid=new Transaction {BranchId=day.Branch.Id,AccountId=day.TabId,ClientId=day.ClientId,TransactionTypeId=request.TransactionTypeId,CategoryId=day.FiadoSaleType.CategoryId,Direction=Direction.Out,Date=day.Today,DueDate=day.Today,Value=50,GoLiveOpeningId=opening.Id,RecordedByOperatorId=day.MemberA.Operator!.Id,CreatedByUserId=day.Manager.User.Id};
        var error=await Should.ThrowAsync<DbUpdateException>(()=>day.QueryAsync(async db=>{db.Transactions.Add(invalid);return await db.SaveChangesAsync();}));
        ((PostgresException)error.InnerException!).ConstraintName.ShouldBe("CK_Transactions_GoLiveOpeningAsset");
        (await day.QueryAsync(db=>db.Transactions.CountAsync(x=>x.BranchId==day.Branch.Id))).ShouldBe(1);
    }

    private static RequestGoLiveOpeningJson Opening(FinancialDayScenario day,bool cheque)=>new() {AccountId=cheque?day.SafeId:day.TabId,ClientId=day.ClientId,IsCheque=cheque,TransactionTypeId=cheque?day.SeededTypes["saidas.deposito-cheque"].Id:day.FiadoSaleType.Id,RecordedByOperatorId=day.MemberA.Operator!.Id,AsOfDate=day.Today,SourceReference="Synthetic initial asset",SourceEvidence="Known opening claim; not income or expense",Items=[new(){Value=125.50m,DueDate=day.Today}]};
}
