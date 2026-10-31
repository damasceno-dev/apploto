using System.Net;
using System.Text.Json;
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
/// M9.6 Phase 4 (4.1–4.7) through HTTP and PostgreSQL. Golden values: opening 1,000 (visible), an inflow of 200, so the
/// expected closing is 1,200; a count of 1,200 differs by 0, a later inflow of 20 makes the unchanged count −20.
/// </summary>
[Collection(ServerApiCollection.Name)]
public class SessionControllerBlindCountTest(ServerWebApplicationFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task BeforeConferir_ReadsAndAutosave_ShouldShowTheOpening_AndNeverTheExpectedClosingOrDifference()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindRead");
        var member = scenario.MemberA.Token;
        await SessionCountScenario.RecordInflowAsync(_client, scenario, member, 200m);

        var save = await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_150m });
        save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
        var saved = (await save.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;
        AssertBlind(saved.Count!, SessionCandidateState.NotConferred);
        saved.Count!.ConfirmationToken.ShouldNotBe(Guid.Empty);
        saved.AllowedActions!.CanConferir.ShouldBeTrue();
        saved.AllowedActions.CanSubmit.ShouldBeFalse();

        // Reload, review and the raw JSON of every read carry no calculation; the opening stays visible.
        var reloaded = (await _client.TryGetSessionAsync(session.Id, member))!;
        AssertBlind(reloaded.Count!, SessionCandidateState.NotConferred);
        reloaded.Count!.ConfirmationToken.ShouldBe(saved.Count!.ConfirmationToken);
        var reviewResponse = await _client.GetAuthAsync($"/session/{session.Id}/review", member);
        var reviewJson = await reviewResponse.Content.ReadAsStringAsync();
        var review = await reviewResponse.ReadContentAsync<ResponseDailyCloseReviewJson>();
        review.Items.Single(item => item.ProductId == scenario.Cash.Id).OpeningValue.ShouldBe(SessionCountScenario.Opening);
        review.Items.Single(item => item.IsCashVarianceProduct).ClosingValue.ShouldBeNull();
        AssertBlind(review.Count, SessionCandidateState.NotConferred);
        AssertNoCalculation(reviewJson);
        AssertNoCalculation(await (await _client.GetAuthAsync($"/session/{session.Id}", member)).Content.ReadAsStringAsync());

        // The retired live preview is gone, and autosave created no attempt or CountConferred event.
        var preview = await _client.PostAuthAsync(
            $"/session/{session.Id}/variance-preview",
            new { Items = new[] { new { ProductId = scenario.Cash.Id, Value = 1_150m } } },
            member);
        preview.StatusCode.ShouldBeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await SessionCountScenario.AttemptsAsync(factory, scenario.Branch.Id, session.Id)).ShouldBeEmpty();
        (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, session.Id, SessionEventKind.CountConferred)).ShouldBeEmpty();
        var history = await (await _client.GetAuthAsync($"/session/{session.Id}/history", member)).ReadContentAsync<ResponseDailyCloseHistoryJson>();
        history.CountAttempts.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task CountSaves_ShouldRotateTheTokenOnlyWhenTheCountChanges()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindTokens");
        var member = scenario.MemberA.Token;
        var values = new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_000m };
        var initialToken = session.Count?.ConfirmationToken
                           ?? (await _client.TryGetSessionAsync(session.Id, member))!.Count!.ConfirmationToken;

        var first = await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, values));
        first.Count!.ConfirmationToken.ShouldNotBe(initialToken);
        first.Custody.CountRevision.ShouldBe(1);

        var identical = await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, values));
        identical.Count!.ConfirmationToken.ShouldBe(first.Count.ConfirmationToken);
        identical.Custody.CountRevision.ShouldBe(1);

        var notesOnly = await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, values, notes: "Conferido com o gerente"));
        notesOnly.Notes.ShouldBe("Conferido com o gerente");
        notesOnly.Count!.ConfirmationToken.ShouldBe(first.Count.ConfirmationToken);
        notesOnly.Custody.CountRevision.ShouldBe(1);
        notesOnly.Custody.LedgerRevision.ShouldBe(0);

        var changed = await Saved(await SessionCountScenario.SaveCountAsync(
            _client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_010m }));
        changed.Count!.ConfirmationToken.ShouldNotBe(first.Count.ConfirmationToken);
        changed.Custody.CountRevision.ShouldBe(2);
    }

    [Fact]
    public async Task Conferir_ShouldRevealTheImmutableCalculation_AndReplayTheSameAttemptForTheSameToken()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindConferir");
        var member = scenario.MemberA.Token;
        await SessionCountScenario.RecordInflowAsync(_client, scenario, member, 200m);
        await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_200m }));
        var read = (await _client.TryGetSessionAsync(session.Id, member))!;

        using var first = await _client.PostConferirAsync(read, member, idempotencyKey: "conferir-click-1");
        first.StatusCode.ShouldBe(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        var conferred = await first.ReadContentAsync<ResponseConferSessionCountJson>();
        conferred.Replayed.ShouldBeFalse();
        var attempt = conferred.Attempt;
        attempt.AttemptNumber.ShouldBe(1);
        attempt.Kind.ShouldBe(SessionCountAttemptKind.Initial);
        attempt.TotalOpening.ShouldBe(1_000m);
        attempt.TotalIn.ShouldBe(200m);
        attempt.TotalOut.ShouldBe(0m);
        attempt.ExpectedClosing.ShouldBe(1_200m);
        attempt.TotalClosing.ShouldBe(1_200m);
        attempt.Difference.ShouldBe(0m);
        attempt.CountRevision.ShouldBe(read.Count!.CountRevision);
        attempt.LedgerRevision.ShouldBe(read.Count.LedgerRevision);
        var cashItem = attempt.Items.Single(item => item.ProductId == scenario.Cash.Id);
        cashItem.OpeningValue.ShouldBe(1_000m);
        cashItem.CountedValue.ShouldBe(1_200m);
        conferred.Session.Count!.CandidateState.ShouldBe(SessionCandidateState.Current);
        conferred.Session.Count.ConfirmedAttempt!.Id.ShouldBe(attempt.Id);
        conferred.Session.AllowedActions!.CanSubmit.ShouldBeTrue();

        // Double click with another key, the same key again, and a retry carrying the old root version: one attempt.
        using var secondKey = await _client.PostConferirAsync(read, member, idempotencyKey: "conferir-click-2");
        using var sameKey = await _client.PostConferirAsync(read, member, idempotencyKey: "conferir-click-1");
        using var oldVersion = await _client.PostConferirAsync(read, member, idempotencyKey: "conferir-click-3", expectedVersion: read.Version);
        foreach (var replay in new[] { secondKey, sameKey, oldVersion })
        {
            replay.StatusCode.ShouldBe(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
            var replayed = await replay.ReadContentAsync<ResponseConferSessionCountJson>();
            replayed.Attempt.Id.ShouldBe(attempt.Id);
            replayed.Replayed.ShouldBeTrue();
        }

        // The same key with a different payload is a key conflict; the used token with other revisions is a token conflict.
        using var keyConflict = await _client.PostConferirAsync(read, member, confirmStillObserved: true, idempotencyKey: "conferir-click-1");
        keyConflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await keyConflict.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.IDEMPOTENCY_KEY_PAYLOAD_CONFLICT]);
        using var tokenConflict = await _client.PostAuthAsync(
            $"/session/{session.Id}/conferir",
            new RequestConferSessionCountJson
            {
                ConfirmationToken = read.Count.ConfirmationToken,
                CountRevision = read.Count.CountRevision + 5,
                LedgerRevision = read.Count.LedgerRevision,
                OpeningRevision = read.Count.OpeningRevision
            },
            member,
            expectedVersion: conferred.Session.Version);
        tokenConflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await tokenConflict.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.SESSION_CONFIRMATION_TOKEN_CONFLICT]);

        (await SessionCountScenario.AttemptsAsync(factory, scenario.Branch.Id, session.Id)).ShouldHaveSingleItem();
        var events = await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, session.Id, SessionEventKind.CountConferred);
        events.ShouldHaveSingleItem().AttemptId.ShouldBe(attempt.Id);

        // Ordinary Conferir → Submit is one step: the confirmed attempt becomes the official snapshot.
        var submit = await _client.PostSubmitAsync(conferred.Session, member);
        submit.StatusCode.ShouldBe(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        (await factory.ReloadAsync<DailyClose>(session.Id))!.SubmittedAttemptId.ShouldBe(attempt.Id);
        var submittedReview = await (await _client.GetAuthAsync($"/session/{session.Id}/review", scenario.Manager.Token))
            .ReadContentAsync<ResponseDailyCloseReviewJson>();
        submittedReview.Items.Single(item => item.IsCashVarianceProduct).ClosingValue.ShouldBe(0m);
        submittedReview.Count.SubmittedAttemptId.ShouldBe(attempt.Id);
    }

    [Fact]
    public async Task ConcurrentConferir_WithDifferentKeys_ShouldResolveToOneAttemptNumberAndEvent()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindRace");
        var member = scenario.MemberA.Token;
        await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 990m }));
        var read = (await _client.TryGetSessionAsync(session.Id, member))!;

        var clients = Enumerable.Range(0, 6).Select(_ => factory.CreateClient()).ToList();
        var responses = await Task.WhenAll(clients.Select((client, index) => client.PostConferirAsync(read, member, idempotencyKey: $"race-{index}")));

        var attemptIds = new HashSet<Guid>();
        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            attemptIds.Add((await response.ReadContentAsync<ResponseConferSessionCountJson>()).Attempt.Id);
        }

        attemptIds.ShouldHaveSingleItem();
        var attempts = await SessionCountScenario.AttemptsAsync(factory, scenario.Branch.Id, session.Id);
        attempts.ShouldHaveSingleItem().AttemptNumber.ShouldBe(1);
        attempts[0].Difference.ShouldBe(-10m);
        (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, session.Id, SessionEventKind.CountConferred)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task LedgerChangeAfterConferir_ShouldShowAnUnconfirmedRecalculation_AndRequireExplicitReconfirmation()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindLedger");
        var member = scenario.MemberA.Token;
        await SessionCountScenario.RecordInflowAsync(_client, scenario, member, 200m);
        await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_200m }));
        var original = await _client.ConferAsync(session.Id, member);
        original.Attempt.Difference.ShouldBe(0m);

        // Count R$1.200, then receive R$20 more: the counted amount may no longer describe the cashbox.
        await SessionCountScenario.RecordInflowAsync(_client, scenario, member, 20m);
        var afterLedger = (await _client.TryGetSessionAsync(session.Id, member))!;
        afterLedger.Count!.LedgerRevision.ShouldBe(original.Session.Count!.LedgerRevision + 1);
        afterLedger.Count.ConfirmationToken.ShouldNotBe(original.Session.Count.ConfirmationToken);
        afterLedger.Count.CandidateState.ShouldBe(SessionCandidateState.Unconfirmed);
        afterLedger.Count.RequiresStillObservedConfirmation.ShouldBeTrue();
        afterLedger.Count.ConfirmedAttempt!.Id.ShouldBe(original.Attempt.Id);
        afterLedger.Count.ConfirmedAttempt.Difference.ShouldBe(0m);
        var recalculation = afterLedger.Count.UnconfirmedRecalculation.ShouldNotBeNull();
        recalculation.ExpectedClosing.ShouldBe(1_220m);
        recalculation.Difference.ShouldBe(-20m);
        afterLedger.AllowedActions!.CanSubmit.ShouldBeFalse();

        // Submit against the old attempt is refused and changes nothing.
        var staleSubmit = await _client.PostSubmitAsync(afterLedger, member, original.Attempt.Id);
        staleSubmit.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await staleSubmit.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.SESSION_CONFIRMATION_NOT_CURRENT]);

        // Without the explicit acknowledgment nothing is consumed: no attempt, same number, same token.
        using var unacknowledged = await _client.PostConferirAsync(afterLedger, member);
        unacknowledged.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await unacknowledged.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([ResourcesErrorMessages.SESSION_CONFIRM_STILL_OBSERVED_REQUIRED]);
        var unchanged = (await _client.TryGetSessionAsync(session.Id, member))!;
        unchanged.Count!.LatestAttemptNumber.ShouldBe(1);
        unchanged.Count.ConfirmationToken.ShouldBe(afterLedger.Count.ConfirmationToken);

        // "Confirmo que esta contagem ainda representa o caixa" creates a new attempt; the original survives intact.
        using var reconfirm = await _client.PostConferirAsync(unchanged, member, confirmStillObserved: true);
        reconfirm.StatusCode.ShouldBe(HttpStatusCode.OK, await reconfirm.Content.ReadAsStringAsync());
        var second = await reconfirm.ReadContentAsync<ResponseConferSessionCountJson>();
        second.Attempt.AttemptNumber.ShouldBe(2);
        second.Attempt.Kind.ShouldBe(SessionCountAttemptKind.StillObserved);
        second.Attempt.Difference.ShouldBe(-20m);
        second.Session.Count!.CandidateState.ShouldBe(SessionCandidateState.Current);

        var attempts = await SessionCountScenario.AttemptsAsync(factory, scenario.Branch.Id, session.Id);
        attempts.Count.ShouldBe(2);
        attempts[0].Id.ShouldBe(original.Attempt.Id);
        attempts[0].Difference.ShouldBe(0m);
        attempts[0].LedgerRevision.ShouldBe(original.Attempt.LedgerRevision);
        (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, session.Id, SessionEventKind.CountConferred)).Count.ShouldBe(2);

        var submit = await _client.PostSubmitAsync(second.Session, member);
        submit.StatusCode.ShouldBe(HttpStatusCode.OK, await submit.Content.ReadAsStringAsync());
        var official = (await factory.ListDailyCloseItemsByDailyCloseIdAsync(session.Id)).Single(item => item.ProductId == scenario.CashVariance.Id);
        official.Value.ShouldBe(-20m);

        // The manager reads every attempt as past evidence.
        var history = await (await _client.GetAuthAsync($"/session/{session.Id}/history", scenario.Manager.Token)).ReadContentAsync<ResponseDailyCloseHistoryJson>();
        history.CountAttempts.ShouldNotBeNull().Select(attempt => attempt.Difference).ShouldBe([0m, -20m]);
    }

    [Fact]
    public async Task Recontar_ShouldHideTheCalculation_AndConfirmIdenticalValuesAsANewAttempt()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindRecount");
        var member = scenario.MemberA.Token;
        await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 980m }));
        var first = await _client.ConferAsync(session.Id, member);
        first.Attempt.Difference.ShouldBe(-20m);

        var recount = await _client.PostAuthAsync(
            $"/session/{session.Id}/recount",
            new RequestBeginSessionRecountJson { PreviousAttemptId = first.Attempt.Id },
            member,
            expectedVersion: first.Session.Version);
        recount.StatusCode.ShouldBe(HttpStatusCode.OK, await recount.Content.ReadAsStringAsync());
        var recounting = await recount.ReadContentAsync<ResponseDailyCloseJson>();
        AssertBlind(recounting.Count!, SessionCandidateState.Recounting);
        recounting.Count!.ConfirmationToken.ShouldNotBe(first.Session.Count!.ConfirmationToken);
        recounting.AllowedActions!.CanSubmit.ShouldBeFalse();
        recounting.AllowedActions.CanRecount.ShouldBeFalse();
        AssertNoCalculation(await (await _client.GetAuthAsync($"/session/{session.Id}", member)).Content.ReadAsStringAsync());

        // Repeating Recontar for the same attempt is a no-op: the token does not rotate again.
        var repeat = await _client.PostAuthAsync(
            $"/session/{session.Id}/recount",
            new RequestBeginSessionRecountJson { PreviousAttemptId = first.Attempt.Id },
            member,
            expectedVersion: recounting.Version);
        repeat.StatusCode.ShouldBe(HttpStatusCode.OK, await repeat.Content.ReadAsStringAsync());
        (await repeat.ReadContentAsync<ResponseDailyCloseJson>()).Count!.ConfirmationToken.ShouldBe(recounting.Count.ConfirmationToken);
        (await SessionCountScenario.EventsAsync(factory, scenario.Branch.Id, session.Id, SessionEventKind.RecountStarted)).ShouldHaveSingleItem();

        // The operator recounts to the same 980 and confirms: a distinct attempt with the same values.
        var second = await _client.ConferAsync(session.Id, member);
        second.Attempt.AttemptNumber.ShouldBe(2);
        second.Attempt.Kind.ShouldBe(SessionCountAttemptKind.Recount);
        second.Attempt.Id.ShouldNotBe(first.Attempt.Id);
        second.Attempt.Difference.ShouldBe(-20m);
        second.Attempt.CountRevision.ShouldBe(first.Attempt.CountRevision);
    }

    [Fact]
    public async Task EditingAfterConferir_ShouldMarkTheResultOutdated_WithoutRevealingANewDifference()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindEdit");
        var member = scenario.MemberA.Token;
        await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_000m }));
        var first = await _client.ConferAsync(session.Id, member);

        var edited = await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_005m }));
        AssertBlind(edited.Count!, SessionCandidateState.Outdated);
        edited.Count!.LatestAttemptId.ShouldBe(first.Attempt.Id);

        var stale = await _client.PostSubmitAsync(edited, member, first.Attempt.Id);
        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var corrected = await _client.ConferAsync(session.Id, member);
        corrected.Attempt.Kind.ShouldBe(SessionCountAttemptKind.Correction);
        corrected.Attempt.Difference.ShouldBe(5m);
    }

    [Fact]
    public async Task UnusedToken_ShouldRequireTheCurrentTokenVersionAndRevisions_AndAFailedConferirShouldConsumeNothing()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindStale");
        var member = scenario.MemberA.Token;
        await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_000m }));
        var read = (await _client.TryGetSessionAsync(session.Id, member))!;

        using var staleVersion = await _client.PostConferirAsync(read, member, expectedVersion: read.Version + 1);
        await ShouldConflict(staleVersion, ResourcesErrorMessages.SESSION_STALE_WRITE);

        using var unknownToken = await _client.PostAuthAsync(
            $"/session/{session.Id}/conferir",
            new RequestConferSessionCountJson
            {
                ConfirmationToken = Guid.NewGuid(),
                CountRevision = read.Count!.CountRevision,
                LedgerRevision = read.Count.LedgerRevision,
                OpeningRevision = read.Count.OpeningRevision
            },
            member,
            expectedVersion: read.Version);
        await ShouldConflict(unknownToken, ResourcesErrorMessages.SESSION_CONFIRMATION_TOKEN_STALE);

        // A Draft row blocks Conferir; the typed count and the token survive and no number is consumed.
        var draft = await _client.PostAuthAsync(
            "/transaction",
            new CommonTestUtilities.Requests.RequestCreateTransactionJsonBuilder()
                .WithAccountId(scenario.Cashbox.Id)
                .WithTransactionTypeId(scenario.InType.Id)
                .WithDate(SessionFlow.LocalToday())
                .WithValue(15m)
                .WithDueDate(null)
                .WithSaveAsDraft(true)
                .Build(),
            member);
        draft.StatusCode.ShouldBe(HttpStatusCode.Created, await draft.Content.ReadAsStringAsync());
        var afterDraft = (await _client.TryGetSessionAsync(session.Id, member))!;
        afterDraft.Count!.ConfirmationToken.ShouldBe(read.Count.ConfirmationToken);
        afterDraft.Count.LedgerRevision.ShouldBe(read.Count.LedgerRevision);
        using var blocked = await _client.PostConferirAsync(afterDraft, member);
        await ShouldConflict(blocked, ResourcesErrorMessages.DAILYCLOSE_OUTSTANDING_DRAFT_TRANSACTIONS);

        var after = (await _client.TryGetSessionAsync(session.Id, member))!;
        after.Count!.LatestAttemptNumber.ShouldBeNull();
        after.Count.ConfirmationToken.ShouldBe(read.Count.ConfirmationToken);
        after.Items.Single(item => item.ProductId == scenario.Cash.Id).Value.ShouldBe(1_000m);
        (await SessionCountScenario.AttemptsAsync(factory, scenario.Branch.Id, session.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Replay_ShouldRequireCurrentAuthority_EvenForAnAlreadyConferredToken()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindAuthority");
        var member = scenario.MemberA.Token;
        await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_000m }));
        var read = (await _client.TryGetSessionAsync(session.Id, member))!;
        using var conferred = await _client.PostConferirAsync(read, member, idempotencyKey: "authority-1");
        conferred.StatusCode.ShouldBe(HttpStatusCode.OK);

        await factory.ExecuteSqlAsync(
            $"UPDATE \"OperatorAccounts\" SET \"Active\" = false WHERE \"OperatorId\" = '{scenario.MemberA.Operator!.Id}' AND \"AccountId\" = '{scenario.Cashbox.Id}'");

        using var sameKey = await _client.PostConferirAsync(read, member, idempotencyKey: "authority-1");
        using var otherKey = await _client.PostConferirAsync(read, member, idempotencyKey: "authority-2");
        foreach (var response in new[] { sameKey, otherKey })
        {
            response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
            var body = await response.Content.ReadAsStringAsync();
            body.ShouldNotContain("difference", Case.Insensitive);
            (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages
                .ShouldBe([ResourcesErrorMessages.TRANSACTION_MEMBER_ACCOUNT_OUT_OF_SCOPE]);
        }
    }

    [Fact]
    public async Task SubmitWithoutConferir_ShouldBeRefused_AndLeaveTheSessionDraft()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindNoConferir");
        var member = scenario.MemberA.Token;
        var saved = await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_000m }));

        var submit = await _client.PostSubmitAsync(saved, member);

        await ShouldConflict(submit, ResourcesErrorMessages.SESSION_CONFIRMATION_NOT_CURRENT);
        var reloaded = (await factory.ReloadAsync<DailyClose>(session.Id))!;
        reloaded.Status.ShouldBe(DailyCloseStatus.Draft);
        reloaded.SubmittedAttemptId.ShouldBeNull();
    }

    [Fact]
    public async Task SubmitAfterLockOrLostGrant_ShouldBeRefused_EvenWithACurrentAttempt()
    {
        var (scenario, session) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindSubmitGuards");
        var member = scenario.MemberA.Token;
        await Saved(await SessionCountScenario.SaveCountAsync(_client, session.Id, member, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_000m }));
        var conferred = await _client.ConferAsync(session.Id, member);

        await factory.ExecuteSqlAsync(
            $"UPDATE \"OperatorAccounts\" SET \"Active\" = false WHERE \"OperatorId\" = '{scenario.MemberA.Operator!.Id}' AND \"AccountId\" = '{scenario.Cashbox.Id}'");
        var revoked = await _client.PostSubmitAsync(conferred.Session, member);
        revoked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        await factory.ExecuteSqlAsync(
            $"UPDATE \"OperatorAccounts\" SET \"Active\" = true WHERE \"OperatorId\" = '{scenario.MemberA.Operator!.Id}' AND \"AccountId\" = '{scenario.Cashbox.Id}'");
        await factory.SeedSettingAsync(scenario.Branch.Id, lockDate: SessionFlow.LocalToday());
        var locked = await _client.PostSubmitAsync(conferred.Session, member);
        await ShouldConflict(locked, ResourcesErrorMessages.DAILYCLOSE_LOCK_DATE_VIOLATION);
        (await factory.ReloadAsync<DailyClose>(session.Id))!.Status.ShouldBe(DailyCloseStatus.Draft);
    }

    [Fact]
    public async Task InheritedOpeningChange_ShouldMakeTheConfirmedResultUnconfirmed_WithoutTouchingTheAttempt()
    {
        var scenario = await SessionScenario.CreateAsync(factory, "BlindOpening");
        var manager = scenario.Manager.Token;
        var today = SessionFlow.LocalToday();
        var predecessor = await Declare(scenario, today.AddDays(-3));
        var successor = await Declare(scenario, today.AddDays(-2));
        await Saved(await SessionCountScenario.SaveCountAsync(_client, predecessor.Id, manager, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 500m }));
        await Saved(await SessionCountScenario.SaveCountAsync(_client, successor.Id, manager, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 500m }));
        var confirmed = await _client.ConferAsync(successor.Id, manager);
        confirmed.Attempt.TotalOpening.ShouldBe(500m);
        confirmed.Attempt.Difference.ShouldBe(0m);

        // Correcting the predecessor's count moves the successor's inherited opening (500 → 520).
        await Saved(await SessionCountScenario.SaveCountAsync(_client, predecessor.Id, manager, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 520m }));
        var after = (await _client.TryGetSessionAsync(successor.Id, manager))!;
        after.Count!.OpeningRevision.ShouldBe(confirmed.Session.Count!.OpeningRevision + 1);
        after.Count.LedgerRevision.ShouldBe(confirmed.Session.Count.LedgerRevision);
        after.Count.CandidateState.ShouldBe(SessionCandidateState.Unconfirmed);
        after.Count.ConfirmedAttempt!.Difference.ShouldBe(0m);
        after.Count.UnconfirmedRecalculation!.TotalOpening.ShouldBe(520m);
        after.Count.UnconfirmedRecalculation.Difference.ShouldBe(-20m);

        var stale = await _client.PostSubmitAsync(after, manager, confirmed.Attempt.Id);
        await ShouldConflict(stale, ResourcesErrorMessages.SESSION_CONFIRMATION_NOT_CURRENT);
        var reconfirmed = await _client.ConferAsync(successor.Id, manager, confirmStillObserved: true);
        reconfirmed.Attempt.Kind.ShouldBe(SessionCountAttemptKind.StillObserved);
        reconfirmed.Attempt.Difference.ShouldBe(-20m);
        (await SessionCountScenario.AttemptsAsync(factory, scenario.Branch.Id, successor.Id))[0].TotalOpening.ShouldBe(500m);
    }

    [Fact]
    public async Task SeparateCashboxes_ShouldKeepTheirRevisionsTokensAndAttemptsApart_ThroughSubmitFinishAndApproval()
    {
        var (scenario, first) = await SessionCountScenario.StartWithOpeningAsync(factory, _client, "BlindSeparate");
        var terminalId = (await factory.ReloadAsync<Account>(scenario.Cashbox.Id))!.TerminalId!.Value;
        var otherCashbox = await scenario.SecondCashboxAsync(factory, terminalId);
        var second = (await _client.StartSessionAsync(otherCashbox.Id, scenario.MemberB.Token)).Session;
        await Saved(await SessionCountScenario.SaveCountAsync(_client, first.Id, scenario.MemberA.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 1_000m }));
        await Saved(await SessionCountScenario.SaveCountAsync(_client, second.Id, scenario.MemberB.Token, new Dictionary<Guid, decimal> { [scenario.Cash.Id] = 0m }));
        var firstConferred = await _client.ConferAsync(first.Id, scenario.MemberA.Token);
        var secondConferred = await _client.ConferAsync(second.Id, scenario.MemberB.Token);

        // An inflow on the first cashbox moves only its own session.
        await SessionCountScenario.RecordInflowAsync(_client, scenario, scenario.MemberA.Token, 30m);
        (await _client.TryGetSessionAsync(first.Id, scenario.MemberA.Token))!.Count!.CandidateState.ShouldBe(SessionCandidateState.Unconfirmed);
        var untouched = (await _client.TryGetSessionAsync(second.Id, scenario.MemberB.Token))!;
        untouched.Count!.CandidateState.ShouldBe(SessionCandidateState.Current);
        untouched.Count.ConfirmationToken.ShouldBe(secondConferred.Session.Count!.ConfirmationToken);

        (await _client.PostSubmitAsync(untouched, scenario.MemberB.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var reconfirmed = await _client.ConferAsync(first.Id, scenario.MemberA.Token, confirmStillObserved: true);
        reconfirmed.Attempt.Difference.ShouldBe(-30m);
        firstConferred.Attempt.Difference.ShouldBe(0m);
        (await _client.PostSubmitAsync(reconfirmed.Session, scenario.MemberA.Token)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await _client.FinishAsync(first.Id, scenario.MemberA.Token);
        await _client.FinishAsync(second.Id, scenario.MemberB.Token);

        foreach (var sessionId in new[] { first.Id, second.Id })
        {
            var approve = await _client.ApproveSessionAsync(sessionId, scenario.Manager.Token);
            approve.StatusCode.ShouldBe(HttpStatusCode.OK, await approve.Content.ReadAsStringAsync());
            (await approve.ReadContentAsync<ResponseDailyCloseJson>()).Custody.CustodyState.ShouldBe(SessionCustodyState.Finished);
        }

        (await factory.ListDailyCloseItemsByDailyCloseIdAsync(first.Id)).Single(item => item.ProductId == scenario.CashVariance.Id).Value.ShouldBe(-30m);
        (await factory.ListDailyCloseItemsByDailyCloseIdAsync(second.Id)).Single(item => item.ProductId == scenario.CashVariance.Id).Value.ShouldBe(0m);
    }

    private async Task<ResponseDailyCloseJson> Declare(SessionScenario scenario, DateTime businessDate)
    {
        var start = DateTime.SpecifyKind(businessDate.AddHours(11), DateTimeKind.Utc);
        var response = await _client.PostAuthAsync(
            "/session/declare",
            new RequestDeclarePastSessionJson
            {
                AccountId = scenario.Cashbox.Id,
                CustodianOperatorId = scenario.MemberA.Operator!.Id,
                StartedAt = start,
                EndedAt = start.AddHours(4),
                Reason = "Turno esquecido"
            },
            scenario.Manager.Token);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.ReadContentAsync<ResponseSessionChainChangeJson>()).Session;
    }

    private static async Task<ResponseDailyCloseJson> Saved(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadContentAsync<ResponsePutDailyCloseItemsJson>()).DailyClose;
    }

    private static async Task ShouldConflict(HttpResponseMessage response, string message)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict, await response.Content.ReadAsStringAsync());
        (await response.ReadContentAsync<TestResponseErrorJson>()).ErrorMessages.ShouldBe([message]);
    }

    private static void AssertBlind(ResponseSessionCountJson count, SessionCandidateState state)
    {
        count.CandidateState.ShouldBe(state);
        count.ConfirmedAttempt.ShouldBeNull();
        count.UnconfirmedRecalculation.ShouldBeNull();
        count.RequiresStillObservedConfirmation.ShouldBeFalse();
    }

    // No expected closing or difference anywhere in a blind body, under any property name.
    private static void AssertNoCalculation(string json)
    {
        using var document = JsonDocument.Parse(json);
        foreach (var name in Names(document.RootElement))
        {
            new[] { "expectedClosing", "difference", "cashVariance" }
                .ShouldNotContain(forbidden => string.Equals(forbidden, name, StringComparison.OrdinalIgnoreCase), name);
        }
    }

    private static IEnumerable<string> Names(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind != JsonValueKind.Null)
                        yield return property.Name;
                    foreach (var nested in Names(property.Value))
                        yield return nested;
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                foreach (var nested in Names(item))
                    yield return nested;
                break;
        }
    }
}
