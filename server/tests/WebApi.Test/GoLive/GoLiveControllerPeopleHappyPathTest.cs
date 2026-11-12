using CommonTestUtilities.Requests;
using System.Net;
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
public class GoLiveControllerPeopleHappyPathTest(ServerWebApplicationFactory factory)
{
    [Theory]
    [InlineData(null)]
    [InlineData("HourBankEntries")]
    [InlineData("TimeEntries")]
    [InlineData("EmployeePayItems")]
    [InlineData("SpecialDraws")]
    public async Task Personnel_import_keeps_pay_hours_attendance_and_draw_origins_separate(string? faultTable)
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 personnel");
        await day.QueryAsync(db => db.TimeEntryPolicies.Where(x => x.BranchId == day.Branch.Id).ExecuteUpdateAsync(x => x
            .SetProperty(p => p.DailyTargetHours, 8m).SetProperty(p => p.LunchDeductionOver6H, 1m).SetProperty(p => p.LunchDeductionOver4H, 0.5m)));
        var categoryResponse = await client.PostAuthAsync("/people/pay-category", new RequestPayCategoryJson
        {
            PostedOn = day.Today,
            Name = "Synthetic salary",
            Kind = PayItemKind.Addition,
            Reason = "Known synthetic category",
            Evidence = "Known synthetic source",
            Enabled = true
        }, day.Manager.Token, Guid.NewGuid().ToString());
        categoryResponse.StatusCode.ShouldBe(HttpStatusCode.Created, await categoryResponse.Content.ReadAsStringAsync());
        var category = await categoryResponse.ReadContentAsync<ResponsePayCategoryJson>();
        var request = RequestGoLiveImportJsonBuilder.BuildMestre();
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblLancamentos", "");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblRegistroPonto", "1,1,2026/11/03 0:00:00,,,,4.5,-3.5,270,-210\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblMovimentacaoBancoHoras", "1,1,2026/11/01 0:00:00,120,Ajuste,Synthetic anchor,\n2,1,2026/11/02 0:00:00,-60,Pagamento,Synthetic hours payment,\n3,1,2026/11/02 0:00:00,30,Ajuste,Synthetic adjustment,\n4,1,2026/11/03 0:00:00,-210,Ponto,Synthetic attendance evidence,1\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblItemRel", "1,Synthetic salary,Addition,1\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblRelMensal", "1,1,100.00,1,11\n");
        RequestGoLiveImportJsonBuilder.ReplaceTable(request, "TblLoteriaEspecial", "1,Synthetic draw,Mega-Sena,2026/11/01 0:00:00,2026/11/30 0:00:00,1000000.00,3000\n");
        var uploadedResponse = await client.PostAuthAsync("/go-live/imports", request, day.Manager.Token, Guid.NewGuid().ToString());
        uploadedResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await uploadedResponse.Content.ReadAsStringAsync());
        var uploaded = await uploadedResponse.ReadContentAsync<ResponseGoLiveImportJson>();
        RequestGoLiveResolutionJson Row(string table, string id, string action) => new()
        {
            Table = table,
            SourceId = id,
            Action = action,
            ExclusionDisposition = action == "Exclude" ? "OutsideScope" : null,
            Reason = "Explicit known synthetic evidence",
            OperatorId = day.MemberA.Operator!.Id,
            PostedOn = day.Today,
            HistoryComplete = true
        };
        var hours = new[] { Row("TblMovimentacaoBancoHoras", "1", "HourOpening"), Row("TblMovimentacaoBancoHoras", "2", "HourPayment"), Row("TblMovimentacaoBancoHoras", "3", "HourAdjustment"), Row("TblMovimentacaoBancoHoras", "4", "AttendanceEvidence") };
        var point = Row("TblRegistroPonto", "1", "Attendance"); point.AttendanceStatus = TimeEntryStatus.Present;
        point.StartsAt = new DateTime(2026, 11, 3, 11, 0, 0, DateTimeKind.Utc); point.EndsAt = new DateTime(2026, 11, 3, 16, 0, 0, DateTimeKind.Utc);
        var pay = Row("TblRelMensal", "1", "PayItem"); pay.PayCategoryId = category.Id; pay.PayKind = PayItemKind.Addition;
        pay.Year = 2026; pay.SourceDate = new DateTime(2026, 11, 1);
        var map = new RequestGoLiveMappingJson { ExpectedRevision = 0, Resolutions = [.. hours, point, pay, Row("TblLoteriaEspecial", "1", "SpecialDraw")] };
        var mapped = await client.PostAuthAsync($"/go-live/imports/{uploaded.Id}/mapping", map, day.Manager.Token, Guid.NewGuid().ToString());
        mapped.StatusCode.ShouldBe(HttpStatusCode.OK, await mapped.Content.ReadAsStringAsync());
        var previewResponse = await client.PostAuthAsync($"/go-live/imports/{uploaded.Id}/preview", new RequestGoLiveMappingJson { ExpectedRevision = 1 }, day.Manager.Token, Guid.NewGuid().ToString());
        previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
        var preview = await previewResponse.ReadContentAsync<ResponseGoLiveImportJson>();
        preview.Preview!.AttendanceComparisons.Single().CalculatedTotalHours.ShouldBe(4.5m);
        preview.Preview.AttendanceComparisons.Single().CalculatedBalanceHours.ShouldBe(-3.5m);
        preview.Preview.AttendanceComparisons.Single().PolicyId.ShouldNotBe(Guid.Empty);
        preview.Preview.CanActivate.ShouldBeTrue(System.Text.Json.JsonSerializer.Serialize(preview.Preview.Issues));
        var confirmation = new RequestActivateGoLiveImportJson { DryRunVersion = preview.Preview.ToolVersion, MappingRevision = 1, PreviewId = preview.PreviewId!.Value, SourceDigest = preview.SourceDigest, TargetFingerprint = preview.TargetFingerprint! };
        var key = Guid.NewGuid().ToString();
        if (faultTable is not null)
        {
            using (factory.Services.GetRequiredService<ArmableCommandInterceptor>().Arm(cmd => cmd.CommandText.Contains($"INSERT INTO \"{faultTable}\"", StringComparison.Ordinal), _ => throw new InvalidOperationException("Synthetic personnel import flush failure")))
                (await client.PostAuthAsync($"/go-live/imports/{uploaded.Id}/activate", confirmation, day.Manager.Token, key)).StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            (await day.QueryAsync(db => db.HourBankEntries.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
            (await day.QueryAsync(db => db.TimeEntries.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
            (await day.QueryAsync(db => db.EmployeePayItems.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
            (await day.QueryAsync(db => db.SpecialDraws.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
            (await day.QueryAsync(db => db.GoLiveImportTargets.CountAsync(x => x.GoLiveImportId == uploaded.Id))).ShouldBe(0);
        }
        var activate = await client.PostAuthAsync($"/go-live/imports/{uploaded.Id}/activate", confirmation, day.Manager.Token, key);
        activate.StatusCode.ShouldBe(HttpStatusCode.OK, await activate.Content.ReadAsStringAsync());
        var result = await activate.ReadContentAsync<ResponseGoLiveImportJson>(); result.TargetIds.Count.ShouldBe(6); result.Targets.Count.ShouldBe(7);
        var entries = await day.QueryAsync(db => db.HourBankEntries.AsNoTracking().Where(x => x.BranchId == day.Branch.Id).ToListAsync());
        entries.Count.ShouldBe(3); entries.Sum(x => x.SignedContribution).ShouldBe(1.5m);
        var recordedAttendance = await day.QueryAsync(db => db.TimeEntries.AsNoTracking().Include(x => x.Segments).SingleAsync(x => x.BranchId == day.Branch.Id));
        recordedAttendance.TotalHours.ShouldBe(4.5m); recordedAttendance.BalanceHours.ShouldBe(-3.5m); recordedAttendance.Segments.Count.ShouldBe(1);
        (await day.QueryAsync(db => db.EmployeePayItems.SingleAsync(x => x.BranchId == day.Branch.Id))).Amount.ShouldBe(100m);
        (await day.QueryAsync(db => db.SpecialDraws.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(1);
        (await day.QueryAsync(db => db.GoLiveImportTargets.CountAsync(x => x.GoLiveImportId == uploaded.Id))).ShouldBe(7);
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
    }
}
