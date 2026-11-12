using CommonTestUtilities.Requests;
using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Infrastructure;
using Shouldly;
using WebApi.Test.Infrastructure;
using WebApi.Test.Sessions;
using Xunit;

namespace WebApi.Test.GoLive;

[Collection(ServerApiCollection.Name)]
public class GoLiveControllerImportUnhappyPathTest(ServerWebApplicationFactory factory)
{
    [Theory]
    [InlineData("encoding")]
    [InlineData("profile")]
    public async Task Same_bytes_with_only_one_changed_interpretation_field_conflict_before_schema_validation(string field)
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 interpretation");
        var request = RequestGoLiveImportJsonBuilder.BuildWindows();
        var original = await Upload(client, day, request);
        var recorded = await day.QueryAsync(db => db.GoLiveImports.AsNoTracking().SingleAsync(x => x.Id == original.Id));
        var originalFiles = request.Files.Select(x => (x.Name, x.Base64)).ToList();
        if (field == "encoding") request.Encoding = "cp1252";
        else request.Profile = "mestre-1.5.0";
        request.Files.Select(x => (x.Name, x.Base64)).ShouldBe(originalFiles);
        request.Encoding.ShouldBe(field == "encoding" ? "cp1252" : "utf-8-sig");
        request.Profile.ShouldBe(field == "profile" ? "mestre-1.5.0" : "windows");
        var changed = await client.PostAuthAsync("/go-live/imports", request, day.Manager.Token, Guid.NewGuid().ToString());
        changed.StatusCode.ShouldBe(HttpStatusCode.Conflict, await changed.Content.ReadAsStringAsync());
        (await changed.Content.ReadAsStringAsync()).ShouldContain(server.Exceptions.ResourcesErrorMessages.GOLIVE_IMPORT_INTERPRETATION_CONFLICT);
        (await day.QueryAsync(db => db.GoLiveImports.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(1);
        var retained = await day.QueryAsync(db => db.GoLiveImports.AsNoTracking().SingleAsync(x => x.Id == original.Id));
        retained.SourceJson.ShouldBe(recorded.SourceJson);
        retained.SourceDigest.ShouldBe(recorded.SourceDigest);
        retained.MappingRevision.ShouldBe(recorded.MappingRevision);
        retained.Status.ShouldBe(recorded.Status);
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id && x.GoLiveOpeningId != null))).ShouldBe(0);
        (await day.QueryAsync(db => db.GoLiveImportAudits.CountAsync(x => x.GoLiveImportId == original.Id))).ShouldBe(1);
        request.Profile = "windows";
        request.Encoding = "utf-8-sig";
        var retry = await Upload(client, day, request);
        retry.Id.ShouldBe(original.Id);
        retry.SourceDigest.ShouldBe(original.SourceDigest);
    }

    [Fact]
    public async Task Ordinary_payment_waits_for_activation_then_settles_the_imported_debt_once()
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 writer boundary");
        var import = await Upload(client, day, RequestGoLiveImportJsonBuilder.BuildWindows());
        (await Map(client, day, import.Id, null, "OpeningFiado", 0)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await Preview(client, day, import.Id, 1);
        var reached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var held = factory.Services.GetRequiredService<ArmableCommandInterceptor>().Arm(
            cmd => cmd.CommandText.Contains("INSERT INTO \"Transactions\"", StringComparison.Ordinal),
            async ct => { reached.TrySetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(20), ct); });
        var activation = client.PostAuthAsync($"/go-live/imports/{import.Id}/activate", Confirm(preview), day.Manager.Token, Guid.NewGuid().ToString());
        Task<HttpResponseMessage>? payment = null;
        var paymentKey = Guid.NewGuid().ToString();
        var request = new RequestReceiveFiadoPaymentJson
        {
            AccountId = day.TabId, ClientId = day.ClientId, Date = day.Today, Value = 25.50m,
            TransactionTypeId = day.PixMethod.Id, RecordedByOperatorId = day.MemberA.Operator!.Id
        };
        try
        {
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(20));
            payment = client.PostAuthAsync("/transaction/fiado-payment", request, day.Manager.Token, paymentKey);
            var lockKey = unchecked((ulong)server.Infrastructure.Services.MonthLockCoordinationKey.Compute(day.Branch.Id));
            var high = (long)(lockKey >> 32);
            var low = (long)(lockKey & uint.MaxValue);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (await day.QueryAsync(db => db.Database.SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_locks WHERE locktype = 'advisory' AND NOT granted AND mode = 'ShareLock' AND classid::bigint = {high} AND objid::bigint = {low}").SingleAsync(timeout.Token)) == 0)
                await Task.Delay(10, timeout.Token);
            payment.IsCompleted.ShouldBeFalse();
            (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id && x.ClientId == day.ClientId))).ShouldBe(0);
        }
        finally { release.TrySetResult(); }
        (await activation).StatusCode.ShouldBe(HttpStatusCode.OK);
        payment.ShouldNotBeNull();
        var result = await payment;
        result.StatusCode.ShouldBe(HttpStatusCode.Created, await result.Content.ReadAsStringAsync());
        var receipt = await result.ReadContentAsync<ResponseCreateTransactionJson>();
        var retry = await client.PostAuthAsync("/transaction/fiado-payment", request, day.Manager.Token, paymentKey);
        (await retry.ReadContentAsync<ResponseCreateTransactionJson>()).Id.ShouldBe(receipt.Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var position = await scope.ServiceProvider.GetRequiredService<server.Domain.Interfaces.ITransactionsRepository>()
            .GetFiadoClientPositionAsNoTracking(day.Branch.Id, day.ClientId, day.Today);
        position.OutstandingTotal.ShouldBe(100m);
        (await day.QueryAsync(db => db.GoLiveImportTargets.CountAsync(x => x.GoLiveImportId == import.Id))).ShouldBe(1);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Precision_remains_unresolved_until_explicit_normalization_or_exclusion(bool normalize)
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 precision");
        var request = RequestGoLiveImportJsonBuilder.BuildWindows();
        var file = request.Files.Single(x => x.Name == "TblLancamentos.csv");
        file.Base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Convert.FromBase64String(file.Base64)).Replace("125.50", "125.500000001")));
        var import = await Upload(client, day, request);
        var mapped = await Map(client, day, import.Id, null, "OpeningFiado", 0);
        mapped.StatusCode.ShouldBe(HttpStatusCode.OK, await mapped.Content.ReadAsStringAsync());
        var preview = await Preview(client, day, import.Id, 1);
        preview.Preview!.CanActivate.ShouldBeFalse();
        preview.Preview.Issues.ShouldContain(x => x.Code == "precision-resolution-required" && x.SourceValue == "125.500000001");
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.GoLiveOpeningId != null && x.BranchId == day.Branch.Id))).ShouldBe(0);
        var normalized = await Map(client, day, import.Id, normalize ? 125.50m : null, normalize ? "OpeningFiado" : "Exclude", 1);
        normalized.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resolved = await Preview(client, day, import.Id, 2);
        resolved.Preview!.Issues.ShouldBeEmpty();
        resolved.Preview.SupportedRows.ShouldBe(normalize ? 1 : 0);
        resolved.Preview.ExcludedRows.ShouldBe(normalize ? 0 : 1);
        // Empty plans never report activation-ready just because every source row was excluded.
        resolved.Preview.CanActivate.ShouldBe(normalize);
    }

    [Fact]
    public async Task Stale_target_refuses_before_any_financial_effect()
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 stale");
        var import = await Upload(client, day, RequestGoLiveImportJsonBuilder.BuildWindows());
        (await Map(client, day, import.Id, null, "OpeningFiado", 0)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await Preview(client, day, import.Id, 1);
        await day.QueryAsync(db => db.Clients.Where(x => x.Id == day.ClientId).ExecuteUpdateAsync(x => x.SetProperty(c => c.Name, "Synthetic changed target")));
        var result = await client.PostAuthAsync($"/go-live/imports/{import.Id}/activate", Confirm(preview), day.Manager.Token, Guid.NewGuid().ToString());
        result.StatusCode.ShouldBe(HttpStatusCode.Conflict, await result.Content.ReadAsStringAsync());
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.GoLiveOpeningId != null && x.BranchId == day.Branch.Id))).ShouldBe(0);
        (await day.QueryAsync(db => db.GoLiveImports.SingleAsync(x => x.Id == import.Id))).Status.ShouldBe("ReviewedPreview");
    }

    [Fact]
    public async Task Flush_failure_rolls_back_targets_audit_and_completion_then_same_key_retries()
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 fault");
        var import = await Upload(client, day, RequestGoLiveImportJsonBuilder.BuildWindows());
        (await Map(client, day, import.Id, null, "OpeningFiado", 0)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await Preview(client, day, import.Id, 1);
        var key = Guid.NewGuid().ToString();
        using (factory.Services.GetRequiredService<ArmableCommandInterceptor>().Arm(
            cmd => cmd.CommandText.Contains("INSERT INTO \"Transactions\"", StringComparison.Ordinal),
            _ => throw new InvalidOperationException("Synthetic M10 flush fault")))
        {
            var failed = await client.PostAuthAsync($"/go-live/imports/{import.Id}/activate", Confirm(preview), day.Manager.Token, key);
            failed.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }
        (await day.QueryAsync(db => db.GoLiveImportTargets.CountAsync(x => x.GoLiveImportId == import.Id))).ShouldBe(0);
        (await day.QueryAsync(db => db.GoLiveImportAudits.CountAsync(x => x.GoLiveImportId == import.Id && x.Action == "Activate"))).ShouldBe(0);
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id && x.GoLiveOpeningId != null))).ShouldBe(0);
        var retry = await client.PostAuthAsync($"/go-live/imports/{import.Id}/activate", Confirm(preview), day.Manager.Token, key);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        (await day.QueryAsync(db => db.GoLiveImportTargets.CountAsync(x => x.GoLiveImportId == import.Id))).ShouldBe(1);
    }

    [Fact]
    public async Task Member_cannot_upload_or_read_private_imports()
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 authority");
        (await client.PostAuthAsync("/go-live/imports", new RequestGoLiveImportJson(), day.MemberA.Token, Guid.NewGuid().ToString())).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAuthAsync("/go-live/imports", day.MemberA.Token)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await client.GetAuthAsync($"/go-live/imports/{Guid.NewGuid()}", day.Manager.Token)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Two_distinct_previews_cannot_both_activate_in_one_branch()
    {
        var client = factory.CreateClient();
        var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 concurrent cutover");
        var request = RequestGoLiveImportJsonBuilder.BuildWindows();
        var first = await Upload(client, day, request);
        var file = request.Files.Single(x => x.Name == "TblLancamentos.csv");
        file.Base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Convert.FromBase64String(file.Base64)).Replace("125.50", "126.50")));
        var second = await Upload(client, day, request);
        (await Map(client, day, first.Id, null, "OpeningFiado", 0)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await Map(client, day, second.Id, null, "OpeningFiado", 0)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var a = await Preview(client, day, first.Id, 1); var b = await Preview(client, day, second.Id, 1);
        a.Preview!.CanActivate.ShouldBeTrue(); b.Preview!.CanActivate.ShouldBeTrue();
        var responses = await Task.WhenAll(client.PostAuthAsync($"/go-live/imports/{first.Id}/activate", Confirm(a), day.Manager.Token, Guid.NewGuid().ToString()), client.PostAuthAsync($"/go-live/imports/{second.Id}/activate", Confirm(b), day.Manager.Token, Guid.NewGuid().ToString()));
        responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        responses.Count(x => x.StatusCode == HttpStatusCode.Conflict).ShouldBe(1);
        (await day.QueryAsync(db => db.GoLiveImports.CountAsync(x => x.BranchId == day.Branch.Id && x.Status == "Activated"))).ShouldBe(1);
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id && x.GoLiveOpeningId != null))).ShouldBe(1);
    }

    [Fact]
    public async Task Mapping_revision_invalidates_review_and_quarantine_never_becomes_ready()
    {
        var client = factory.CreateClient(); var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 quarantined");
        var imported = await Upload(client, day, RequestGoLiveImportJsonBuilder.BuildWindows());
        (await Map(client, day, imported.Id, null, "OpeningFiado", 0)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var reviewed = await Preview(client, day, imported.Id, 1);
        var mapping = new RequestGoLiveMappingJson { ExpectedRevision = 1, Resolutions = [new() { Table = "TblLancamentos", SourceId = "1", Action = "Exclude", ExclusionDisposition = "UnresolvedHistory", Reason = "Custody and origin unavailable; retain readonly evidence" }] };
        var key = Guid.NewGuid().ToString();
        var changed = await client.PostAuthAsync($"/go-live/imports/{imported.Id}/mapping", mapping, day.Manager.Token, key);
        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAuthAsync($"/go-live/imports/{imported.Id}/activate", Confirm(reviewed), day.Manager.Token, Guid.NewGuid().ToString())).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        mapping.Resolutions[0].Reason = "Changed intent under the same key";
        (await client.PostAuthAsync($"/go-live/imports/{imported.Id}/mapping", mapping, day.Manager.Token, key)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var blocked = await Preview(client, day, imported.Id, 2);
        blocked.Preview!.CanActivate.ShouldBeFalse(); blocked.Preview.QuarantinedRows.ShouldBe(1);
        blocked.Preview.ExcludedRows.ShouldBe(0); blocked.Preview.Issues.ShouldContain(x => x.Code == "quarantined-history");
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
    }

    [Fact]
    public async Task Imported_individual_debt_prevents_a_second_manual_opening_for_the_same_client_account()
    {
        var client = factory.CreateClient(); var day = await FinancialDayScenario.CreateAsync(factory, client, "M10 predecessor debt");
        var import = await Upload(client, day, RequestGoLiveImportJsonBuilder.BuildWindows());
        (await Map(client, day, import.Id, null, "ImportedDebt", 0)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await Preview(client, day, import.Id, 1); preview.Preview!.CanActivate.ShouldBeTrue();
        (await client.PostAuthAsync($"/go-live/imports/{import.Id}/activate", Confirm(preview), day.Manager.Token, Guid.NewGuid().ToString())).StatusCode.ShouldBe(HttpStatusCode.OK);
        var duplicate = await client.PostAuthAsync("/go-live/openings", new RequestGoLiveOpeningJson { AccountId = day.TabId, ClientId = day.ClientId, AsOfDate = day.Today, RecordedByOperatorId = day.MemberA.Operator!.Id, TransactionTypeId = day.FiadoSaleType.Id, SourceReference = "duplicate synthetic opening", SourceEvidence = "Must not sum with the imported predecessor", Items = [new() { Value = 125.50m, DueDate = day.Today }] }, day.Manager.Token, Guid.NewGuid().ToString());
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await day.QueryAsync(db => db.Transactions.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(1);
        (await day.QueryAsync(db => db.GoLiveOpenings.CountAsync(x => x.BranchId == day.Branch.Id))).ShouldBe(0);
    }

    private static async Task<ResponseGoLiveImportJson> Upload(HttpClient client, FinancialDayScenario day, RequestGoLiveImportJson request)
    {
        var response = await client.PostAuthAsync("/go-live/imports", request, day.Manager.Token, Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseGoLiveImportJson>();
    }

    private static Task<HttpResponseMessage> Map(HttpClient client, FinancialDayScenario day, Guid id, decimal? amount, string action, int revision) =>
        client.PostAuthAsync($"/go-live/imports/{id}/mapping", new RequestGoLiveMappingJson
        {
            ExpectedRevision = revision,
            Resolutions = [new() {
            Table = "TblLancamentos", SourceId = "1", Action = action, ExclusionDisposition = action == "Exclude" ? "OutsideScope" : null, Reason = "Explicit synthetic resolution",
            HistoryComplete=true, AccountId = day.TabId, ClientId = day.ClientId, OperatorId = day.MemberA.Operator!.Id, TransactionTypeId = day.FiadoSaleType.Id, TargetValue = amount }]
        }, day.Manager.Token, Guid.NewGuid().ToString());

    private static async Task<ResponseGoLiveImportJson> Preview(HttpClient client, FinancialDayScenario day, Guid id, int revision)
    {
        var response = await client.PostAuthAsync($"/go-live/imports/{id}/preview", new RequestGoLiveMappingJson { ExpectedRevision = revision }, day.Manager.Token, Guid.NewGuid().ToString());
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.ReadContentAsync<ResponseGoLiveImportJson>();
    }

    private static RequestActivateGoLiveImportJson Confirm(ResponseGoLiveImportJson preview) => new()
    {
        DryRunVersion = preview.Preview!.ToolVersion,
        MappingRevision = preview.MappingRevision,
        PreviewId = preview.PreviewId!.Value,
        SourceDigest = preview.SourceDigest,
        TargetFingerprint = preview.TargetFingerprint!
    };
}
