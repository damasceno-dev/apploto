using System.Net;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Domain.Entities.Enums;
using Shouldly;

namespace WebApi.Test.Infrastructure;

/// <summary>
/// Blind count and approval steps through the API (M9.6 Phases 4–5): Conferir with the server-issued token and
/// revisions, submit naming the confirmed attempt, and approval with the session's version.
/// </summary>
internal static class SessionCountTestHelpers
{
    extension(HttpClient client)
    {
        public async Task<ResponseDailyCloseJson?> TryGetSessionAsync(Guid sessionId, string token)
        {
            using var response = await client.GetAuthAsync($"/session/{sessionId}", token);
            return response.StatusCode == HttpStatusCode.OK
                ? await response.ReadContentAsync<ResponseDailyCloseJson>()
                : null;
        }

        public Task<HttpResponseMessage> PostConferirAsync(
            ResponseDailyCloseJson read,
            string token,
            bool confirmStillObserved = false,
            string? idempotencyKey = null,
            uint? expectedVersion = null)
        {
            read.Count.ShouldNotBeNull();
            return client.PostAuthAsync(
                $"/session/{read.Id}/conferir",
                new RequestConferSessionCountJson
                {
                    ConfirmationToken = read.Count!.ConfirmationToken,
                    CountRevision = read.Count.CountRevision,
                    LedgerRevision = read.Count.LedgerRevision,
                    OpeningRevision = read.Count.OpeningRevision,
                    ConfirmCountStillObserved = confirmStillObserved
                },
                token,
                idempotencyKey,
                expectedVersion ?? read.Version);
        }

        public async Task<ResponseConferSessionCountJson> ConferAsync(Guid sessionId, string token, bool confirmStillObserved = false)
        {
            var read = await client.TryGetSessionAsync(sessionId, token);
            read.ShouldNotBeNull();
            using var response = await client.PostConferirAsync(read, token, confirmStillObserved);
            response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
            return await response.ReadContentAsync<ResponseConferSessionCountJson>();
        }

        public Task<HttpResponseMessage> PostSubmitAsync(ResponseDailyCloseJson read, string token, Guid? attemptId = null)
        {
            return client.PostAuthAsync(
                $"/session/{read.Id}/submit",
                new RequestSubmitSessionJson
                {
                    AttemptId = attemptId ?? read.Count?.LatestAttemptId ?? Guid.Empty,
                    CountRevision = read.Count?.CountRevision ?? read.Custody.CountRevision,
                    LedgerRevision = read.Count?.LedgerRevision ?? read.Custody.LedgerRevision,
                    OpeningRevision = read.Count?.OpeningRevision ?? read.Custody.OpeningRevision
                },
                token,
                expectedVersion: read.Version);
        }

        /// <summary>
        /// The ordinary path, Conferir → Submit: confers first when the session is a counted Draft without a current
        /// attempt (a refused Conferir is left to the submit, whose own preconditions report first), then submits the
        /// latest attempt with the session's revisions and version.
        /// </summary>
        public async Task<HttpResponseMessage> SubmitSessionAsync(Guid sessionId, string token)
        {
            var read = await client.TryGetSessionAsync(sessionId, token);
            if (read is null)
            {
                return await client.PostAuthAsync(
                    $"/session/{sessionId}/submit",
                    new RequestSubmitSessionJson(),
                    token,
                    expectedVersion: 1);
            }

            if (read.Status is DailyCloseStatus.Draft or DailyCloseStatus.Rejected
                && read.ItemsFirstRecordedAt is not null
                && read.Count!.CandidateState != SessionCandidateState.Current)
            {
                using var confer = await client.PostConferirAsync(read, token);
                if (confer.StatusCode == HttpStatusCode.OK)
                    read = (await confer.ReadContentAsync<ResponseConferSessionCountJson>()).Session;
            }

            return await client.PostSubmitAsync(read, token);
        }

        public async Task<HttpResponseMessage> ApproveSessionAsync(
            Guid sessionId,
            string token,
            SessionApprovalMode mode = SessionApprovalMode.Independent,
            string? reason = null,
            string? idempotencyKey = null,
            uint? expectedVersion = null)
        {
            var version = expectedVersion ?? (await client.TryGetSessionAsync(sessionId, token))?.Version ?? 1;
            return await client.PostAuthAsync(
                $"/session/{sessionId}/approve",
                new RequestApproveSessionJson { Mode = mode, Reason = reason },
                token,
                idempotencyKey,
                version);
        }

        /// <summary>Approval by the branch's only manager of a session they recorded or submitted themselves.</summary>
        public Task<HttpResponseMessage> ApproveAsSingleManagerAsync(Guid sessionId, string token)
        {
            return client.ApproveSessionAsync(sessionId, token, SessionApprovalMode.SingleActiveManager, "Único gestor da filial");
        }
    }
}
