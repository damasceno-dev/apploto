namespace server.Domain.Entities.Enums;

/// <summary>How the session came to exist; it fixes which opening evidence the session may carry.</summary>
public enum SessionOrigin
{
    Started = 0,
    HandoverAccepted = 1,
    ManagerException = 2,
    ManagerDeclared = 3,
    Migrated = 4
}
