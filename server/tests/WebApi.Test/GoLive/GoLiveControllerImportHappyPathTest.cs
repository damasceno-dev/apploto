using CommonTestUtilities.Requests;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Domain.Interfaces;
using server.Communication.Requests;
using server.Communication.Responses;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;
using Xunit;

namespace WebApi.Test.GoLive;

[Collection(ServerApiCollection.Name)]
public class GoLiveControllerImportHappyPathTest(ServerWebApplicationFactory factory)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reviewed_import_persists_mapping_without_preview_money_and_activates_once(bool cheque)
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 import");
        var request = RequestGoLiveImportJsonBuilder.BuildWindows();
        var upload = await client.PostAuthAsync("/go-live/imports", request, day.Manager.Token, Guid.NewGuid().ToString());
        upload.StatusCode.ShouldBe(HttpStatusCode.OK, await upload.Content.ReadAsStringAsync());
        var imported = await upload.ReadContentAsync<ResponseGoLiveImportJson>();
        var again = await client.PostAuthAsync("/go-live/imports", request, day.Manager.Token, Guid.NewGuid().ToString());
        (await again.ReadContentAsync<ResponseGoLiveImportJson>()).Id.ShouldBe(imported.Id);
        var map = new RequestGoLiveMappingJson
        {
            ExpectedRevision = 0,
            Resolutions = [new() {
            Table = "TblLancamentos", SourceId = "1", Action = cheque ? "OpeningCheque" : "OpeningFiado", Reason = "Known synthetic opening debt",
            AccountId = cheque ? day.SafeId : day.TabId, ClientId = day.ClientId, OperatorId = day.MemberA.Operator!.Id, TransactionTypeId = cheque ? day.SeededTypes["saidas.deposito-cheque"].Id : day.FiadoSaleType.Id }]
        };
        var mapped = await client.PostAuthAsync($"/go-live/imports/{imported.Id}/mapping", map, day.Manager.Token, Guid.NewGuid().ToString());
        mapped.StatusCode.ShouldBe(HttpStatusCode.OK, await mapped.Content.ReadAsStringAsync());
        var count = await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id));
        var previewResponse = await client.PostAuthAsync($"/go-live/imports/{imported.Id}/preview", new RequestGoLiveMappingJson { ExpectedRevision = 1 }, day.Manager.Token, Guid.NewGuid().ToString());
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
        var preview = await previewResponse.ReadContentAsync<ResponseGoLiveImportJson>();
        preview.Preview!.CanActivate.ShouldBeTrue();
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(count);
        var confirmation = new RequestActivateGoLiveImportJson
        {
            DryRunVersion = preview.Preview!.ToolVersion,
            MappingRevision = 1,
            SourceDigest = preview.SourceDigest,
            PreviewId = preview.PreviewId!.Value,
            TargetFingerprint = preview.TargetFingerprint!
        };
        var key = Guid.NewGuid().ToString();
        var activate = await client.PostAuthAsync($"/go-live/imports/{imported.Id}/activate", confirmation, day.Manager.Token, key);
        activate.StatusCode.ShouldBe(HttpStatusCode.OK, await activate.Content.ReadAsStringAsync());
        var activated = await activate.ReadContentAsync<ResponseGoLiveImportJson>();
        activated.Status.ShouldBe("Activated");
        activated.TargetIds.Count.ShouldBe(1);
        activated.Targets.Single().TargetId.ShouldBe(activated.TargetIds.Single());
        activated.Targets.Single().SourceTable.ShouldBe("TblLancamentos");
        activated.Targets.Single().SourceId.ShouldBe("1");
        activated.Targets.Single().TargetKind.ShouldBe(cheque ? "OpeningCheque" : "OpeningFiado");
        var retry = await client.PostAuthAsync($"/go-live/imports/{imported.Id}/activate", confirmation, day.Manager.Token, key);
        (await retry.ReadContentAsync<ResponseGoLiveImportJson>()).TargetIds.ShouldBe(activated.TargetIds);
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(count + 1);
        var row = await day.QueryAsync(db => db.Transactions.AsNoTracking().SingleAsync(x => x.Id == activated.TargetIds[0]));
        row.SessionId.ShouldBeNull(); row.Value.ShouldBe(125.50m); row.GoLiveOpeningId.ShouldNotBeNull();
        // Import ownership protects the immutable source/target registry even before FIFO consumption.
        var listedOpenings = await client.GetAuthAsync("/go-live/openings", day.Manager.Token);
        listedOpenings.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await listedOpenings.ReadContentAsync<List<ResponseGoLiveOpeningJson>>()).Single(x => x.Id == row.GoLiveOpeningId).SourceImportId.ShouldBe(imported.Id);
        var importedOpening = await day.QueryAsync(db => db.GoLiveOpenings.AsNoTracking().SingleAsync(x => x.Id == row.GoLiveOpeningId));
        var cancelImported = await client.PostAuthAsync($"/go-live/openings/{importedOpening.Id}/cancel",
            new RequestCancelGoLiveOpeningJson { Reason = "Attempt manual cancellation before repayment" },
            day.Manager.Token, Guid.NewGuid().ToString(), expectedVersion: importedOpening.Version);
        cancelImported.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await cancelImported.Content.ReadAsStringAsync()).ShouldContain(server.Exceptions.ResourcesErrorMessages.GOLIVE_OPENING_IMPORT_OWNED);
        (await day.QueryAsync(db => db.GoLiveOpenings.AsNoTracking().SingleAsync(x => x.Id == importedOpening.Id))).Active.ShouldBeTrue();
        (await day.QueryAsync(db => db.Transactions.AsNoTracking().SingleAsync(x => x.Id == row.Id))).Status.ShouldBe(row.Status);
        var amendImported = await client.PostAuthAsync($"/go-live/openings/{importedOpening.Id}/amend",
            new RequestAmendGoLiveOpeningJson { Reason = "Attempt manual amendment before repayment", Opening = preview.Preview.OpeningPlan.Single() },
            day.Manager.Token, Guid.NewGuid().ToString(), expectedVersion: importedOpening.Version);
        amendImported.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await amendImported.Content.ReadAsStringAsync()).ShouldContain(server.Exceptions.ResourcesErrorMessages.GOLIVE_OPENING_IMPORT_OWNED);
        if (cheque) return;
        var repayment = await client.PostAuthAsync("/transaction/fiado-payment", new RequestReceiveFiadoPaymentJson
        {
            AccountId = day.TabId,
            ClientId = day.ClientId,
            Date = day.Today,
            Value = 25.50m,
            TransactionTypeId = day.PixMethod.Id,
            RecordedByOperatorId = day.MemberA.Operator!.Id
        }, day.Manager.Token, Guid.NewGuid().ToString());
        repayment.StatusCode.ShouldBe(HttpStatusCode.Created, await repayment.Content.ReadAsStringAsync());
        var receipt = await repayment.ReadContentAsync<ResponseCreateTransactionJson>();
        receipt.GeneratedTransactionId.ShouldBeNull(); receipt.SessionId.ShouldBeNull();
        await using var scope = factory.Services.CreateAsyncScope();
        var position = await scope.ServiceProvider.GetRequiredService<ITransactionsRepository>().GetFiadoClientPositionAsNoTracking(day.Branch.Id, day.ClientId, day.Today);
        position.OutstandingTotal.ShouldBe(100m);
        var openingId = row.GoLiveOpeningId!.Value;
        var refused = await client.PostAuthAsync($"/go-live/openings/{openingId}/cancel", new RequestCancelGoLiveOpeningJson { Reason = "Must refuse after FIFO consumption" }, day.Manager.Token, Guid.NewGuid().ToString(), expectedVersion: (await day.QueryAsync(db => db.GoLiveOpenings.AsNoTracking().SingleAsync(x => x.Id == openingId))).Version);
        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);

    }

}
