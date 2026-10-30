using server.Application.Services.DailyCloses;
using server.Communication.Requests;
using server.Communication.Responses;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.Application.UseCases.Transactions;

/// <summary>Shared session-binding rules of the guarded financial creates (§6.20 RecordSessionOperation).</summary>
internal static class TransactionSessionBinding
{
    /// <summary>A session id or opening acknowledgment is meaningful only when the command writes a cashbox row.</summary>
    public static void EnsureUsed(
        Guid? sessionId,
        RequestSessionOpeningAcknowledgmentJson? acknowledgment,
        bool writesCashboxRow)
    {
        if (writesCashboxRow is false && (sessionId is not null || acknowledgment is not null))
            throw new OnValidationException([ResourcesErrorMessages.SESSION_ONLY_ON_CASHBOX]);
    }

    public static void Apply(ResponseCreateTransactionJson response, params SessionRowResolution?[] resolutions)
    {
        var started = resolutions.FirstOrDefault(resolution => resolution?.Started is not null)?.Started;
        response.StartedSessionId = started?.Session.Id;
        response.OpeningDiscrepancyId = started?.Discrepancy?.Id;
    }
}
