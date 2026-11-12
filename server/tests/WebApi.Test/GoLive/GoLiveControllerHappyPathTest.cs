using System.Net;
using Xunit;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;

namespace WebApi.Test.GoLive;

[Collection(ServerApiCollection.Name)]
public class GoLiveControllerHappyPathTest(ServerWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Opening_is_once_only_without_session_or_counterpart(bool cheque)
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 opening");
        var countBefore = await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id));
        var request = new RequestGoLiveOpeningJson
        {
            AccountId = cheque ? day.SafeId : day.TabId,
            ClientId = cheque ? null : day.ClientId,
            TransactionTypeId = cheque ? day.SeededTypes["saidas.deposito-cheque"].Id : day.FiadoSaleType.Id,
            RecordedByOperatorId = day.MemberA.Operator!.Id,
            AsOfDate = day.Today,
            IsCheque = cheque,
            SourceReference = "synthetic-1",
            SourceEvidence = "Synthetic known opening, no private source",
            Items = [new() { Value = 125.50m, DueDate = day.Today.AddDays(10) }]
        };
        var key = Guid.NewGuid().ToString();
        var response = await client.PostAuthAsync("/go-live/openings", request, day.Manager.Token, key);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var saved = await response.ReadContentAsync<ResponseGoLiveOpeningJson>();
        saved.TransactionIds.Count.ShouldBe(1);
        saved.SourceImportId.ShouldBeNull();
        var listed = await client.GetAuthAsync("/go-live/openings", day.Manager.Token);
        (await listed.ReadContentAsync<List<ResponseGoLiveOpeningJson>>()).Single(x => x.Id == saved.Id).SourceImportId.ShouldBeNull();
        var retry = await client.PostAuthAsync("/go-live/openings", request, day.Manager.Token, key);
        (await retry.ReadContentAsync<ResponseGoLiveOpeningJson>()).Id.ShouldBe(saved.Id);
        var duplicate = await client.PostAuthAsync("/go-live/openings", request, day.Manager.Token, Guid.NewGuid().ToString());
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var rows = await day.QueryAsync(db => db.Transactions.AsNoTracking().Where(x => x.GoLiveOpeningId == saved.Id).ToListAsync());
        rows.Single().SessionId.ShouldBeNull();
        rows.Single().GeneratedFromTransactionId.ShouldBeNull();
        rows.Single().Direction.ShouldBe(Direction.Out);
        rows.Single().PostedSettlementInstrument.ShouldBe(cheque ? SettlementInstrument.Cheque : SettlementInstrument.None);
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(countBefore + 1);
        var cancelled = await client.PostAuthAsync($"/go-live/openings/{saved.Id}/cancel", new RequestCancelGoLiveOpeningJson { Reason = "Synthetic correction" }, day.Manager.Token, Guid.NewGuid().ToString(), expectedVersion: saved.Version);
        cancelled.StatusCode.ShouldBe(HttpStatusCode.OK, await cancelled.Content.ReadAsStringAsync());
        (await day.QueryAsync(db => db.Transactions.SingleAsync(x => x.Id == saved.TransactionIds[0]))).Status.ShouldBe(TransactionStatus.Cancelled);
    }
}
