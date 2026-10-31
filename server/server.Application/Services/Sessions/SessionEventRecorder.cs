using System.Text.Json;
using server.Domain.Entities;
using server.Domain.Entities.Enums;
using server.Domain.Interfaces;

namespace server.Application.Services.Sessions;

public sealed class SessionEventRecorder(ISessionEventsRepository sessionEventsRepository)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task Record(
        DailyClose session,
        SessionEventKind kind,
        User actor,
        DateTime now,
        object? details = null,
        string? reason = null,
        Guid? relatedId = null,
        CancellationToken ct = default,
        Guid? attemptId = null)
    {
        return sessionEventsRepository.Add(new SessionEvent
        {
            BranchId = session.BranchId,
            SessionId = session.Id,
            Kind = kind,
            ActorUserId = actor.Id,
            ActorName = actor.Name,
            Reason = reason,
            RelatedId = relatedId,
            AttemptId = attemptId,
            CreatedAt = now,
            Details = JsonSerializer.Serialize(details ?? new { }, JsonOptions)
        }, ct);
    }
}
