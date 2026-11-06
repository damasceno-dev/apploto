using server.Communication.Responses;
using server.Domain.Entities;

namespace server.Application.UseCases.Sessions.Adjustment;

internal static class OpeningAdjustmentMapper
{
    extension(OpeningDiscrepancyAdjustment record)
    {
        public ResponseOpeningDiscrepancyAdjustmentJson ToResponse() => new()
        {
            Id = record.Id, DiscrepancyId = record.DiscrepancyId, ComparisonRevision = record.ComparisonRevision,
            Sequence = record.Sequence, PreviousAdjustmentId = record.PreviousAdjustmentId, PriorAttributionId = record.PriorAttributionId,
            SessionId = record.SessionId, SourceDate = record.SourceDate, PostedOn = record.PostedOn, Target = record.Target,
            PreviousOperatorId = record.PreviousOperatorId, PreviousSignedAmount = record.PreviousSignedAmount,
            OperatorId = record.OperatorId, SignedAmount = record.SignedAmount,
            ActorUserId = record.ActorUserId, CreatedAt = record.CreatedAt, Reason = record.Reason
        };
    }
}
