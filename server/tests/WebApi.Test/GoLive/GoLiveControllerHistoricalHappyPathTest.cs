using CommonTestUtilities.Requests;
using System.Net;
using System.Text;
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
public class GoLiveControllerHistoricalHappyPathTest(ServerWebApplicationFactory factory)
{
    [Fact]
    public async Task Explicit_counted_history_proposes_last_count_without_invented_acknowledgment_or_official_variance()
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 counted history");
        var cashboxResponse = await client.PostAuthAsync("/account/terminal", new RequestCreateTerminalAccountJson { Name = "Synthetic imported box" }, day.Manager.Token);
        cashboxResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var cashbox = await cashboxResponse.ReadContentAsync<ResponseCreateAccountJson>();
        var request = RequestGoLiveImportJsonBuilder.BuildWindows();
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblLancamentos", "");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblEstoque", "1,2026/11/01 0:00:00,6,100.00,1,2026/11/01 0:00:00\n2,2026/11/02 0:00:00,6,110.00,1,2026/11/02 0:00:00\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblContas", "1,Synthetic source box,,,Terminal\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblProduto", "6,Dinheiro sintético\n");
        var uploadedResponse = await client.PostAuthAsync("/go-live/imports", request, day.Manager.Token, Guid.NewGuid().ToString());
        uploadedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await uploadedResponse.Content.ReadAsStringAsync());
        var uploaded = await uploadedResponse.ReadContentAsync<ResponseGoLiveImportJson>();
        var map = new RequestGoLiveMappingJson
        {
            ExpectedRevision = 0,
            Resolutions = [
            Count("1", 1, cashbox, day), Count("2", 2, cashbox, day)]
        };
        var mapped = await client.PostAuthAsync($"/go-live/imports/{uploaded.Id}/mapping", map, day.Manager.Token, Guid.NewGuid().ToString());
        mapped.StatusCode.ShouldBe(HttpStatusCode.OK, await mapped.Content.ReadAsStringAsync());
        var previewResponse = await client.PostAuthAsync($"/go-live/imports/{uploaded.Id}/preview", new RequestGoLiveMappingJson { ExpectedRevision = 1 }, day.Manager.Token, Guid.NewGuid().ToString());
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
        var preview = await previewResponse.ReadContentAsync<ResponseGoLiveImportJson>();
        preview.Preview!.CanActivate.ShouldBeTrue(System.Text.Json.JsonSerializer.Serialize(preview.Preview.Issues));
        var activate = await client.PostAuthAsync($"/go-live/imports/{uploaded.Id}/activate", new RequestActivateGoLiveImportJson
        {
            DryRunVersion = preview.Preview!.ToolVersion,
            MappingRevision = 1,
            PreviewId = preview.PreviewId!.Value,
            SourceDigest = preview.SourceDigest,
            TargetFingerprint = preview.TargetFingerprint!
        }, day.Manager.Token, Guid.NewGuid().ToString());
        activate.StatusCode.ShouldBe(HttpStatusCode.OK, await activate.Content.ReadAsStringAsync());
        var sessions = await day.QueryAsync(db => db.DailyCloses.AsNoTracking().Include(x => x.Items).Where(x => x.AccountId == cashbox.Id).OrderBy(x => x.Sequence).ToListAsync());
        sessions.Count.ShouldBe(2);
        sessions.ShouldAllBe(x => x.MappingState == SessionMappingState.Ready && x.CustodyState == SessionCustodyState.Finished
            && x.Origin == SessionOrigin.Migrated && x.Status == DailyCloseStatus.Draft && !x.ReviewHistoryComplete);
        sessions[1].Items.Single().Value.ShouldBe(110m);
        await factory.SeedOperatorAccountAsync(day.MemberA.Operator!.Id, cashbox.Id, isPrimary: false);
        var proposalResponse = await client.GetAuthAsync($"/session/opening-proposal?accountId={cashbox.Id}", day.MemberA.Token);
        proposalResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await proposalResponse.Content.ReadAsStringAsync());
        var proposal = await proposalResponse.ReadContentAsync<ResponseSessionOpeningProposalJson>();
        proposal.CanStart.ShouldBeTrue(); proposal.HasUnresolvedHistory.ShouldBeFalse();
        proposal.SourceKind.ShouldBe(SessionOpeningSourceKind.PreviousSession);
        proposal.SourceId.ShouldBe(sessions[1].Id);
        proposal.Items.Single(x => x.ProductId == day.Cash.Id).Value.ShouldBe(110m);
        var start = await client.PostAuthAsync("/session/start", new RequestStartSessionJson { AccountId = cashbox.Id, Opening = new() { SourceKind = proposal.SourceKind, SourceId = proposal.SourceId, SourceRevision = proposal.SourceRevision, Items = [new() { ProductId = day.Cash.Id, Value = 110m }] } }, day.MemberA.Token);
        start.StatusCode.ShouldBe(HttpStatusCode.Created, await start.Content.ReadAsStringAsync());
        var started = await start.ReadContentAsync<ResponseStartSessionJson>();
        started.Session.Custody.Sequence.ShouldBe(3);
        (await day.QueryAsync(db => db.SessionOpenings.AsNoTracking().SingleAsync(x => x.Id == started.SessionOpeningId))).SourceId.ShouldBe(sessions[1].Id);
        var counted = await client.CountAsync(started.Session.Id, day.MemberA.Token, new Dictionary<Guid, decimal> { [day.Cash.Id] = 110m });
        var submitted = await client.SubmitAsync(started.Session.Id, day.MemberA.Token);
        submitted.Items.Single(x => x.ProductId == day.CashVariance.Id).Value.ShouldBe(0m);
        (await day.QueryAsync(db => db.SessionOpenings.CountAsync(x => x.Session.AccountId == cashbox.Id && x.SessionId != started.Session.Id))).ShouldBe(0);
        (await day.QueryAsync(db => db.DailyCloseItems.CountAsync(x => x.DailyClose.AccountId == cashbox.Id && x.DailyCloseId != started.Session.Id && x.ProductId == day.CashVariance.Id))).ShouldBe(0);
        (await day.QueryAsync(db => db.GoLiveImportTargets.CountAsync(x => x.GoLiveImportId == uploaded.Id))).ShouldBe(2);
    }

    private static RequestGoLiveResolutionJson Count(string id, int sequence, ResponseCreateAccountJson box, FinancialDayScenario day) => new()
    {
        Table = "TblEstoque",
        SourceId = id,
        Action = "CountedSession",
        Reason = "Synthetic complete source count and custody boundary",
        AccountId = box.Id,
        TerminalId = box.TerminalId,
        OperatorId = day.MemberA.Operator!.Id,
        ProductId = day.Cash.Id,
        Sequence = sequence,
        HistoryComplete = true,
        StartsAt = new DateTime(2026, 11, sequence, 11, 0, 0, DateTimeKind.Utc),
        EndsAt = new DateTime(2026, 11, sequence, 20, 0, 0, DateTimeKind.Utc)
    };

}
