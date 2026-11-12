using System.Net;
using CommonTestUtilities.Requests;
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
public class GoLiveControllerMissingEvidenceTest(ServerWebApplicationFactory factory)
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Missing_year_or_custodian_is_a_blocking_resolution_without_operational_writes(bool missingYear)
    {
        var client = factory.CreateClient(); var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 missing facts");
        var request = missingYear ? RequestGoLiveImportJsonBuilder.BuildMestre() : RequestGoLiveImportJsonBuilder.BuildWindows();
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblLancamentos", "");
        RequestGoLiveResolutionJson resolution;
        if (missingYear)
        {
            RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblRelMensal", "1,1,100.00,1,11\n");
            resolution = new() { Table = "TblRelMensal", SourceId = "1", Action = "PayItem", OperatorId = day.MemberA.Operator!.Id, PayKind = PayItemKind.Addition, SourceDate = new DateTime(2026, 11, 1), PostedOn = day.Today, HistoryComplete = true, Reason = "Year genuinely unavailable; do not infer it" };
        }
        else
        {
            var create = await client.PostAuthAsync("/account/terminal", new RequestCreateTerminalAccountJson { Name = "Missing historical custody" }, day.Manager.Token);
            var box = await create.ReadContentAsync<ResponseCreateAccountJson>();
            RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblEstoque", "1,2026/11/01 0:00:00,6,100.00,1,2026/11/01 0:00:00\n");
            resolution = new() { Table = "TblEstoque", SourceId = "1", Action = "CountedSession", AccountId = box.Id, TerminalId = box.TerminalId, ProductId = day.Cash.Id, Sequence = 1, HistoryComplete = true, StartsAt = new DateTime(2026, 11, 1, 11, 0, 0, DateTimeKind.Utc), EndsAt = new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc), Reason = "Custodian unavailable; account name is not evidence" };
        }
        var uploadedResponse = await client.PostAuthAsync("/go-live/imports", request, day.Manager.Token, Guid.NewGuid().ToString());
        var uploaded = await uploadedResponse.ReadContentAsync<ResponseGoLiveImportJson>();
        (await client.PostAuthAsync($"/go-live/imports/{uploaded.Id}/mapping", new RequestGoLiveMappingJson { ExpectedRevision = 0, Resolutions = [resolution] }, day.Manager.Token, Guid.NewGuid().ToString())).StatusCode.ShouldBe(HttpStatusCode.OK);
        var response = await client.PostAuthAsync($"/go-live/imports/{uploaded.Id}/preview", new RequestGoLiveMappingJson { ExpectedRevision = 1 }, day.Manager.Token, Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var preview = await response.ReadContentAsync<ResponseGoLiveImportJson>();
        preview.Preview!.CanActivate.ShouldBeFalse();
        preview.Preview.Issues.ShouldContain(x => x.Code == (missingYear ? "pay-year-required" : "history-resolution-required"));
        (await day.QueryAsync(db => db.EmployeePayItems.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
        (await day.QueryAsync(db => db.DailyCloses.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
        (await day.QueryAsync(db => db.GoLiveImports.AsNoTracking().SingleAsync(x => x.Id == uploaded.Id))).Status.ShouldBe("NeedsResolution");
    }
}
