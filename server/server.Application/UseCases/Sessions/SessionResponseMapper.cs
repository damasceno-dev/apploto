using server.Communication.Responses;
using server.Domain.Entities;
using server.Domain.Entities.Enums;

namespace server.Application.UseCases.Sessions;

public static class SessionResponseMapper
{
    extension(DailyClose session)
    {
        public ResponseSessionCustodyJson ToCustodyResponse()
        {
            return new ResponseSessionCustodyJson
            {
                MappingState = session.MappingState,
                Origin = session.Origin,
                CustodyState = session.CustodyState,
                Sequence = session.Sequence,
                CustodianOperatorId = session.CustodianOperatorId,
                CustodianOperatorName = session.CustodianOperator?.Name,
                TerminalId = session.TerminalId,
                StartedAt = session.StartedAt,
                EndedAt = session.EndedAt,
                CountRevision = session.CountRevision,
                LedgerRevision = session.LedgerRevision,
                OpeningRevision = session.OpeningRevision,
                OpeningAcknowledged = session.Opening is not null,
                CustodyHistoryComplete = session.CustodyHistoryComplete
            };
        }
    }

    extension(SessionOpeningDiscrepancy discrepancy)
    {
        public ResponseOpeningDiscrepancyJson ToResponse()
        {
            var current = discrepancy.Revisions.FirstOrDefault(revision => revision.Revision == discrepancy.CurrentRevision);
            List<OpeningDiscrepancyAttribution> targets = discrepancy.PredecessorSessionId is null
                ? [OpeningDiscrepancyAttribution.Incoming, OpeningDiscrepancyAttribution.Neither]
                : [OpeningDiscrepancyAttribution.Outgoing, OpeningDiscrepancyAttribution.Incoming, OpeningDiscrepancyAttribution.Neither];

            return new ResponseOpeningDiscrepancyJson
            {
                Id = discrepancy.Id,
                Version = discrepancy.Version,
                SessionId = discrepancy.SessionId,
                SessionOpeningId = discrepancy.SessionOpeningId,
                HandoverId = discrepancy.HandoverId,
                PredecessorSessionId = discrepancy.PredecessorSessionId,
                SourceKind = discrepancy.SourceKind,
                CurrentRevision = discrepancy.CurrentRevision,
                CurrentTotalDelta = discrepancy.CurrentTotalDelta,
                Attribution = discrepancy.Attribution,
                AttributedRevision = discrepancy.AttributedRevision,
                CountsTowardPerson = discrepancy.Attribution is OpeningDiscrepancyAttribution.Outgoing
                                         or OpeningDiscrepancyAttribution.Incoming
                                     && discrepancy.AttributedRevision == discrepancy.CurrentRevision
                                     && discrepancy.CurrentTotalDelta != 0m,
                AllowedTargets = discrepancy.CurrentTotalDelta == 0m ? [] : targets,
                Items = current?.Items
                    .OrderBy(item => item.ProductId)
                    .Select(item => new ResponseOpeningDiscrepancyItemJson
                    {
                        ProductId = item.ProductId,
                        ProposedValue = item.ProposedValue,
                        AcknowledgedValue = item.AcknowledgedValue,
                        Delta = item.Delta
                    })
                    .ToList() ?? []
            };
        }
    }

    extension(SessionHandover handover)
    {
        public ResponseSessionHandoverJson ToResponse()
        {
            return new ResponseSessionHandoverJson
            {
                Id = handover.Id,
                Version = handover.Version,
                AccountId = handover.AccountId,
                OutgoingSessionId = handover.OutgoingSessionId,
                IncomingSessionId = handover.IncomingSessionId,
                IntendedIncomingOperatorId = handover.IntendedIncomingOperatorId,
                IntendedIncomingOperatorName = handover.IntendedIncomingOperator?.Name ?? string.Empty,
                State = handover.State,
                OfferedCountRevision = handover.OfferedCountRevision,
                OfferedAttemptId = handover.OfferedAttemptId,
                OfferedByUserId = handover.OfferedByUserId,
                OfferedAt = handover.OfferedAt,
                ResolvedByUserId = handover.ResolvedByUserId,
                ResolvedAt = handover.ResolvedAt,
                Reason = handover.Reason,
                Items = handover.Items
                    .OrderBy(item => item.ProductId)
                    .Select(item => new ResponseSessionHandoverItemJson { ProductId = item.ProductId, Value = item.Value })
                    .ToList()
            };
        }
    }

    extension(SessionEvent sessionEvent)
    {
        public ResponseSessionEventJson ToResponse()
        {
            return new ResponseSessionEventJson
            {
                Id = sessionEvent.Id,
                Kind = sessionEvent.Kind,
                ActorUserId = sessionEvent.ActorUserId,
                ActorName = sessionEvent.ActorName,
                Reason = sessionEvent.Reason,
                RelatedId = sessionEvent.RelatedId,
                CreatedAt = sessionEvent.CreatedAt,
                Details = sessionEvent.Details
            };
        }
    }
}
