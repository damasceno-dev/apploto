namespace server.Domain.Entities.Enums;

public enum SessionEventKind
{
    SessionStarted = 0,
    OpeningAcknowledged = 1,
    OpeningDiscrepancyOpened = 2,
    OpeningDiscrepancyRevised = 3,
    HandoverOffered = 4,
    HandoverAccepted = 5,
    HandoverDisputed = 6,
    HandoverWithdrawn = 7,
    SessionFinished = 8,
    CustodyExceptionResolved = 9,
    SessionDeclared = 10,
    SessionRearranged = 11,
    AttributionRecorded = 12
}
