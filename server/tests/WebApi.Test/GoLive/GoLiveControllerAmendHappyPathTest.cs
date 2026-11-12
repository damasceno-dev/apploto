using System.Net;
using Microsoft.EntityFrameworkCore;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;
using Xunit;

namespace WebApi.Test.GoLive;

[Collection(ServerApiCollection.Name)]
public class GoLiveControllerAmendHappyPathTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Amendment_appends_revision_and_plan_keeps_every_real_receivable()
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 amendment");
        var request = new RequestGoLiveOpeningJson
        {
            AccountId = day.SafeId,
            IsCheque = true,
            TransactionTypeId = day.SeededTypes["saidas.deposito-cheque"].Id,
            RecordedByOperatorId = day.MemberA.Operator!.Id,
            AsOfDate = day.Today,
            SourceReference = "synthetic-plan",
            SourceEvidence = "Synthetic evidenced opening plan",
            Items = [new() { Value = 50, DueDate = day.Today.AddDays(10) }, new() { Value = 75, DueDate = day.Today.AddDays(20) }]
        };
        var response = await client.PostAuthAsync("/go-live/openings", request, day.Manager.Token, Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var original = await response.ReadContentAsync<ResponseGoLiveOpeningJson>();
        original.Version.ShouldBeGreaterThan(0u);
        var rows = await day.QueryAsync(db => db.Transactions.AsNoTracking().Where(x => x.GoLiveOpeningId == original.Id).OrderBy(x => x.DueDate).ToListAsync());
        rows.Count.ShouldBe(2);
        rows.ShouldAllBe(x => x.OriginTransactionId == rows[0].Id && x.SessionId == null);
        request.Items[0].Value = 60;
        var amendment = new RequestAmendGoLiveOpeningJson { Opening = request, Reason = "Correct synthetic amount before any settlement" };
        var key = Guid.NewGuid().ToString();
        var amendedResponse = await client.PostAuthAsync($"/go-live/openings/{original.Id}/amend", amendment, day.Manager.Token, key, original.Version);
        amendedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await amendedResponse.Content.ReadAsStringAsync());
        var amended = await amendedResponse.ReadContentAsync<ResponseGoLiveOpeningJson>();
        amended.PreviousId.ShouldBe(original.Id);
        amended.Items.Sum(x => x.Value).ShouldBe(135m);
        (await day.QueryAsync(db => db.Transactions.AsNoTracking().Where(x => x.GoLiveOpeningId == original.Id).ToListAsync())).ShouldAllBe(x => x.Status == TransactionStatus.Cancelled);
        var retry = await client.PostAuthAsync($"/go-live/openings/{original.Id}/amend", amendment, day.Manager.Token, key, original.Version);
        (await retry.ReadContentAsync<ResponseGoLiveOpeningJson>()).Id.ShouldBe(amended.Id);
        (await day.QueryAsync(db => db.GoLiveOpenings.CountAsync(x => x.BranchId == day.Branch.Id && x.Active))).ShouldBe(1);
    }
}
