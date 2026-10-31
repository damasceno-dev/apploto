using System.Net;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Exceptions;
using Shouldly;
using WebApi.Test.Infrastructure;
using Xunit;

namespace WebApi.Test.Sessions;

/// <summary>
/// M9.6 Phase 7 (§6.19 O08, §6.20 field-save semantics): Relatório do terminal (signed), Tarifa Bolão and Tarifa
/// Especial on the authoritative count save — one generated effect per field and session, versioned history, the
/// ledger revision and Conferir candidate moving with every changed effect, and nothing written by reads.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class SessionControllerCloseFieldsTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task SignedReportAndFees_ShouldReconcileTheE09Walkthrough_InBothSigns()
    {
        // E09: O=1,000; R=+300; Bolão 35; Especial 15, all received in cash → C=1,350; I=350 U=0 → 0.
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "FieldsE09");
        var sessionId = await s.StartAsync();
        var before = await _client.GetSessionAsync(sessionId, s.MemberA.Token);

        var saved = await s.SaveOkAsync(sessionId, cash: 1_350m, fields: FinancialDayScenario.Fields(300m, 35m, 15m));

        saved.Count!.LedgerRevision.ShouldBe(before.Count!.LedgerRevision + 1);
        saved.Count.ConfirmationToken.ShouldNotBe(before.Count.ConfirmationToken);
        saved.Count.ConfirmedAttempt.ShouldBeNull();
        saved.CloseFields!.Select(f => (f.Field, f.Value, f.Revision)).ShouldBe([
            (SessionCloseField.TerminalReport, 300m, 1), (SessionCloseField.BolaoFee, 35m, 1), (SessionCloseField.SpecialFee, 15m, 1)
        ]);
        var rows = (await s.RowsAsync()).Where(r => r.SessionId == sessionId).ToList();
        rows.Count.ShouldBe(3);
        AssertEffect(rows, s, TransactionGenerationKind.SessionTerminalReport, TransactionTypeSystemRole.TerminalReportIn, Direction.In, 300m);
        AssertEffect(rows, s, TransactionGenerationKind.SessionBolaoFee, TransactionTypeSystemRole.BolaoFee, Direction.In, 35m);
        AssertEffect(rows, s, TransactionGenerationKind.SessionSpecialFee, TransactionTypeSystemRole.SpecialFee, Direction.In, 15m);
        rows.ShouldAllBe(r => r.RecordedByOperatorId == s.MemberA.Operator!.Id && r.Status == TransactionStatus.Active);

        var attempt = (await _client.ConferAsync(sessionId, s.MemberA.Token)).Attempt;
        (attempt.TotalOpening, attempt.TotalIn, attempt.TotalOut, attempt.Difference).ShouldBe((1_000m, 350m, 0m, 0m));

        // Hypothetical R = −60 with the same fees: C=990; I=50 U=60 → 0. The old In is cancelled, an Out of 60 replaces it.
        await s.SaveOkAsync(sessionId, cash: 990m, fields: FinancialDayScenario.Fields(report: -60m));
        var negative = (await _client.ConferAsync(sessionId, s.MemberA.Token)).Attempt;
        (negative.TotalIn, negative.TotalOut, negative.Difference).ShouldBe((50m, 60m, 0m));

        var reportRows = (await s.RowsAsync()).Where(r => r.GenerationKind == TransactionGenerationKind.SessionTerminalReport).ToList();
        reportRows.Count.ShouldBe(2);
        reportRows.ShouldAllBe(r => r.Value > 0m);
        var cancelled = reportRows.Single(r => r.Status == TransactionStatus.Cancelled);
        (cancelled.Direction, cancelled.Value, cancelled.CancelledByUserId).ShouldBe((Direction.In, 300m, s.MemberA.User.Id));
        var live = reportRows.Single(r => r.Status == TransactionStatus.Active);
        (live.Direction, live.Value, live.TransactionTypeId).ShouldBe((Direction.Out, 60m, s.SystemTypes[TransactionTypeSystemRole.TerminalReportOut].Id));

        var history = await Revisions(s, sessionId, SessionCloseField.TerminalReport);
        history.Select(h => (h.Revision, h.PreviousValue, h.Value, h.CancelledTransactionId, h.CreatedTransactionId)).ShouldBe([
            (1, 0m, 300m, (Guid?)null, (Guid?)cancelled.Id),
            (2, 300m, -60m, cancelled.Id, live.Id)
        ]);
    }

    [Fact]
    public async Task OmittedFieldsAndIdenticalValues_ShouldWriteNothing_AndKeepTheCandidate()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "FieldsOmit");
        var sessionId = await s.StartAsync();
        await s.SaveOkAsync(sessionId, cash: 1_035m, fields: FinancialDayScenario.Fields(bolao: 35m));
        var confirmed = (await _client.ConferAsync(sessionId, s.MemberA.Token)).Session;

        // Omitted fields preserve; the same value again is no change; Notes-only is no change either.
        var omitted = await s.SaveOkAsync(sessionId, cash: 1_035m);
        var identical = await s.SaveOkAsync(sessionId, cash: 1_035m, fields: FinancialDayScenario.Fields(bolao: 35m));
        var notes = await s.SaveAsync(sessionId, s.MemberA.Token, identical.Version, 1_035m, notes: "Conferido com o borderô");
        notes.StatusCode.ShouldBe(HttpStatusCode.OK, await notes.Content.ReadAsStringAsync());

        foreach (var read in new[] { omitted, identical, (await notes.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose })
        {
            read.Count!.LedgerRevision.ShouldBe(confirmed.Count!.LedgerRevision);
            read.Count.ConfirmationToken.ShouldBe(confirmed.Count.ConfirmationToken);
            read.Count.CandidateState.ShouldBe(SessionCandidateState.Current);
            read.CloseFields!.Single(f => f.Field == SessionCloseField.BolaoFee).Revision.ShouldBe(1);
        }
        (await s.RowsAsync()).Count(r => r.SessionId == sessionId).ShouldBe(1);
    }

    [Fact]
    public async Task Zero_ShouldCancelTheEffect_KeepTheHistory_AndBeIdempotent()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "FieldsZero");
        var sessionId = await s.StartAsync();
        await s.SaveOkAsync(sessionId, cash: 1_015m, fields: FinancialDayScenario.Fields(special: 15m));

        var zeroed = await s.SaveOkAsync(sessionId, cash: 1_000m, fields: FinancialDayScenario.Fields(special: 0m));
        var again = await s.SaveOkAsync(sessionId, cash: 1_000m, fields: FinancialDayScenario.Fields(special: 0m));

        var field = again.CloseFields!.Single(f => f.Field == SessionCloseField.SpecialFee);
        (field.Value, field.Revision, field.TransactionId).ShouldBe((0m, 2, (Guid?)null));
        again.Count!.LedgerRevision.ShouldBe(zeroed.Count!.LedgerRevision);
        var row = (await s.RowsAsync()).Single(r => r.SessionId == sessionId);
        row.Status.ShouldBe(TransactionStatus.Cancelled);
        row.CancellationReason.ShouldNotBeNullOrWhiteSpace();
        (await Revisions(s, sessionId, SessionCloseField.SpecialFee)).Select(h => (h.PreviousValue, h.Value)).ShouldBe([(0m, 15m), (15m, 0m)]);
        (await _client.ConferAsync(sessionId, s.MemberA.Token)).Attempt.Difference.ShouldBe(0m);
    }

    [Theory]
    [InlineData("{\"field\":\"BolaoFee\",\"value\":null}", nameof(ResourcesErrorMessages.SESSION_CLOSE_FIELD_VALUE_REQUIRED))]
    [InlineData("{\"field\":\"BolaoFee\"}", nameof(ResourcesErrorMessages.SESSION_CLOSE_FIELD_VALUE_REQUIRED))]
    [InlineData("{\"field\":\"SpecialFee\",\"value\":-1}", nameof(ResourcesErrorMessages.SESSION_CLOSE_FIELD_FEE_NEGATIVE))]
    [InlineData("{\"field\":\"TerminalReport\",\"value\":10.005}", nameof(ResourcesErrorMessages.SESSION_CLOSE_FIELD_VALUE_PRECISION))]
    [InlineData("{\"field\":\"TerminalReport\",\"value\":-1000000000000}", nameof(ResourcesErrorMessages.SESSION_CLOSE_FIELD_VALUE_PRECISION))]
    [InlineData("{\"value\":10}", nameof(ResourcesErrorMessages.SESSION_CLOSE_FIELD_INVALID))]
    [InlineData("{\"field\":\"BolaoFee\",\"value\":1},{\"field\":\"BolaoFee\",\"value\":2}", nameof(ResourcesErrorMessages.SESSION_CLOSE_FIELD_DUPLICATE))]
    public async Task InvalidField_ShouldBeRefusedWithoutAnyEffect(string fieldsJson, string key)
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "FieldsInvalid");
        var sessionId = await s.StartAsync();
        var current = await _client.GetSessionAsync(sessionId, s.MemberA.Token);
        var body = $"{{\"items\":[{{\"productId\":\"{s.Cash.Id}\",\"value\":1000}}],\"closeFields\":[{fieldsJson}]}}";

        using var request = new HttpRequestMessage(HttpMethod.Put, $"/session/{sessionId}/items")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new("Bearer", s.MemberA.Token);
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{current.Version}\"");
        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        var expected = typeof(ResourcesErrorMessages).GetProperty(key)!.GetValue(null) as string;
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(expected!);
        (await s.RowsAsync()).ShouldBeEmpty();
        (await _client.GetSessionAsync(sessionId, s.MemberA.Token)).Version.ShouldBe(current.Version);
    }

    [Fact]
    public async Task StaleIfMatch_AndConcurrentSaves_ShouldNeverDuplicateAnEffect()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "FieldsRace");
        var sessionId = await s.StartAsync();
        var current = await _client.GetSessionAsync(sessionId, s.MemberA.Token);

        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(i =>
            s.SaveAsync(sessionId, s.MemberA.Token, current.Version, 1_000m + i, fields: FinancialDayScenario.Fields(bolao: 35m + i))));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        foreach (var refused in responses.Where(r => r.StatusCode != HttpStatusCode.OK))
        {
            refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
            (await refused.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldHaveSingleItem();
        }
        var rows = (await s.RowsAsync()).Where(r => r.SessionId == sessionId).ToList();
        rows.ShouldHaveSingleItem().GenerationKind.ShouldBe(TransactionGenerationKind.SessionBolaoFee);
        (await Revisions(s, sessionId, SessionCloseField.BolaoFee)).ShouldHaveSingleItem();

        // Replaying the stale version is refused and writes nothing.
        var stale = await s.SaveAsync(sessionId, s.MemberA.Token, current.Version, 1_000m, fields: FinancialDayScenario.Fields(bolao: 99m));
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await stale.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(ResourcesErrorMessages.DAILYCLOSE_STALE_WRITE);
        (await s.RowsAsync()).Count(r => r.SessionId == sessionId).ShouldBe(1);
    }

    [Fact]
    public async Task Database_ShouldRefuseASecondLiveEffectOrASlotForAnotherSession()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "FieldsDb");
        var sessionId = await s.StartAsync();
        await s.SaveOkAsync(sessionId, cash: 1_035m, fields: FinancialDayScenario.Fields(bolao: 35m));
        var effect = (await s.RowsAsync()).Single(r => r.SessionId == sessionId);

        var duplicate = await Should.ThrowAsync<PostgresException>(() => factory.ExecuteSqlAsync($"""
            INSERT INTO "Transactions" ("Id", "Date", "Value", "TransactionTypeId", "CategoryId", "Direction", "AccountId",
                "DueDate", "RecordedByOperatorId", "CreatedByUserId", "Status", "BranchId", "GenerationKind", "SessionId",
                "Active", "CreatedAt")
            SELECT gen_random_uuid(), "Date", 1, "TransactionTypeId", "CategoryId", "Direction", "AccountId", "DueDate",
                "RecordedByOperatorId", "CreatedByUserId", 1, "BranchId", "GenerationKind", "SessionId", true, now()
            FROM "Transactions" WHERE "Id" = '{effect.Id}'
            """));
        duplicate.ConstraintName.ShouldBe("IX_Transactions_SessionId_GenerationKind_LiveFieldEffect");

        var detached = await Should.ThrowAsync<PostgresException>(() => factory.ExecuteSqlAsync($"""
            UPDATE "SessionFieldEffects" SET "Field" = 3 WHERE "CurrentTransactionId" = '{effect.Id}'
            """));
        detached.ConstraintName.ShouldBe("CK_SessionFieldEffects_SlotImmutable");

        var rewritten = await Should.ThrowAsync<PostgresException>(() => factory.ExecuteSqlAsync("""
            UPDATE "SessionFieldEffectRevisions" SET "Value" = 1
            """));
        rewritten.ConstraintName.ShouldBe("CK_SessionFieldEffectRevisions_Immutable");
    }

    [Fact]
    public async Task GeneratedFieldRowsAndTheirTypes_ShouldRefuseEveryManualPath()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "FieldsManual");
        var sessionId = await s.StartAsync();
        await s.SaveOkAsync(sessionId, cash: 1_300m, fields: FinancialDayScenario.Fields(report: 300m));
        var effect = (await s.RowsAsync()).Single(r => r.SessionId == sessionId);

        var cancel = await _client.PostAuthAsync(
            $"/transaction/{effect.Id}/cancel",
            new RequestCancelTransactionJson { CancellationReason = "Tentativa manual" },
            s.Manager.Token,
            expectedVersion: effect.Version);
        await ShouldFail(cancel, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_GENERATED_ROW_READ_ONLY);

        foreach (var role in new[] { TransactionTypeSystemRole.TerminalReportIn, TransactionTypeSystemRole.TerminalReportOut,
                     TransactionTypeSystemRole.BolaoFee, TransactionTypeSystemRole.SpecialFee })
        {
            var manual = await _client.PostAuthAsync(
                "/transaction",
                new RequestCreateTransactionJson { Date = s.Today, Value = 5m, TransactionTypeId = s.SystemTypes[role].Id, AccountId = s.CashboxId },
                s.MemberA.Token);
            await ShouldFail(manual, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_SYSTEM_ONLY);

            var edit = await _client.PutAuthAsync(
                $"/transaction-type/{s.SystemTypes[role].Id}",
                new RequestUpdateTransactionTypeJson { Name = "Renomeado", SettlementRule = SettlementRule.SameDay },
                s.Manager.Token);
            await ShouldFail(edit, HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_SYSTEM_READ_ONLY);
            await ShouldFail(await _client.DeleteAuthAsync($"/transaction-type/{s.SystemTypes[role].Id}", s.Manager.Token),
                HttpStatusCode.Conflict, ResourcesErrorMessages.TRANSACTION_TYPE_SYSTEM_READ_ONLY);
        }

        // Receita hosts system fee types but stays open for custom types (only automatic-leg categories are closed).
        var receita = s.SystemTypes[TransactionTypeSystemRole.BolaoFee].CategoryId;
        var ordinary = await _client.PostAuthAsync(
            "/transaction-type",
            new RequestCreateTransactionTypeJson { CategoryId = receita, Name = "Outra receita", SettlementRule = SettlementRule.SameDay },
            s.Manager.Token);
        ordinary.StatusCode.ShouldBe(HttpStatusCode.Created, await ordinary.Content.ReadAsStringAsync());
        (await s.RowsAsync()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task FieldChangeAfterConferir_ShouldInvalidateTheAttempt_UntilTheCurrentRevisionIsConfirmed()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "FieldsConferir");
        var sessionId = await s.StartAsync();
        await s.SaveOkAsync(sessionId, cash: 1_035m, fields: FinancialDayScenario.Fields(bolao: 35m));
        var first = await _client.ConferAsync(sessionId, s.MemberA.Token);
        first.Attempt.Difference.ShouldBe(0m);

        // The fee was really 45: a ledger change, so the unchanged count becomes Unconfirmed (never official).
        var changed = await s.SaveOkAsync(sessionId, cash: 1_035m, fields: FinancialDayScenario.Fields(bolao: 45m));
        changed.Count!.CandidateState.ShouldBe(SessionCandidateState.Unconfirmed);
        changed.Count.LedgerRevision.ShouldBe(first.Session.Count!.LedgerRevision + 1);
        changed.Count.UnconfirmedRecalculation!.Difference.ShouldBe(-10m);

        var staleSubmit = await _client.PostSubmitAsync(changed, s.MemberA.Token, first.Attempt.Id);
        await ShouldFail(staleSubmit, HttpStatusCode.Conflict, ResourcesErrorMessages.SESSION_CONFIRMATION_NOT_CURRENT);

        // The operator recounts 1,045 and confirms against the current revisions; submission then succeeds.
        await s.SaveOkAsync(sessionId, cash: 1_045m);
        var second = await _client.ConferAsync(sessionId, s.MemberA.Token);
        (second.Attempt.TotalIn, second.Attempt.Difference).ShouldBe((45m, 0m));
        var submitted = await _client.SubmitAsync(sessionId, s.MemberA.Token);
        submitted.Status.ShouldBe(DailyCloseStatus.Submitted);
        submitted.Items.Single(item => item.ProductId == s.CashVariance.Id).Value.ShouldBe(0m);

        // Submitted freezes the fields with the ledger.
        var frozen = await s.SaveAsync(sessionId, s.MemberA.Token, submitted.Version, 1_045m, fields: FinancialDayScenario.Fields(bolao: 50m));
        frozen.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await s.RowsAsync()).Count(r => r.SessionId == sessionId && r.Status == TransactionStatus.Active).ShouldBe(1);
    }

    [Fact]
    public async Task LockedDate_AndReads_ShouldWriteNothing()
    {
        var s = await FinancialDayScenario.CreateAsync(factory, _client, "FieldsLock");
        var sessionId = await s.StartAsync();
        await s.SaveOkAsync(sessionId, cash: 1_035m, fields: FinancialDayScenario.Fields(bolao: 35m));
        var before = await _client.GetSessionAsync(sessionId, s.Manager.Token);

        var review = await _client.GetAuthAsync($"/session/{sessionId}/review", s.Manager.Token);
        review.StatusCode.ShouldBe(HttpStatusCode.OK, await review.Content.ReadAsStringAsync());
        (await review.ReadContentAsync<ResponseDailyCloseReviewJson>()).CloseFields
            .Single(f => f.Field == SessionCloseField.BolaoFee).Value.ShouldBe(35m);
        (await _client.GetSessionAsync(sessionId, s.Manager.Token)).Version.ShouldBe(before.Version);

        await factory.ExecuteSqlAsync($"""
            UPDATE "Settings" SET "LockDate" = '{s.Today:yyyy-MM-dd}' WHERE "BranchId" = '{s.Branch.Id}'
            """);
        var locked = await s.SaveAsync(sessionId, s.MemberA.Token, before.Version, 1_035m, fields: FinancialDayScenario.Fields(bolao: 0m));

        await ShouldFail(locked, HttpStatusCode.Conflict, ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        (await s.RowsAsync()).Single(r => r.SessionId == sessionId).Status.ShouldBe(TransactionStatus.Active);
    }

    private static void AssertEffect(
        IReadOnlyList<Transaction> rows,
        FinancialDayScenario s,
        TransactionGenerationKind kind,
        TransactionTypeSystemRole role,
        Direction direction,
        decimal value)
    {
        var row = rows.Single(r => r.GenerationKind == kind);
        row.AccountId.ShouldBe(s.CashboxId);
        row.TransactionTypeId.ShouldBe(s.SystemTypes[role].Id);
        row.Direction.ShouldBe(direction);
        row.Value.ShouldBe(value);
        row.Date.ShouldBe(s.Today);
        row.GeneratedFromTransactionId.ShouldBeNull();
    }

    private static Task<List<SessionFieldEffectRevision>> Revisions(FinancialDayScenario s, Guid sessionId, SessionCloseField field) =>
        s.QueryAsync(db => db.SessionFieldEffectRevisions.AsNoTracking()
            .Where(r => r.SessionFieldEffect.SessionId == sessionId && r.SessionFieldEffect.Field == field)
            .OrderBy(r => r.Revision)
            .ToListAsync());

    private static async Task ShouldFail(HttpResponseMessage response, HttpStatusCode status, string key)
    {
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldContain(key);
    }
}
