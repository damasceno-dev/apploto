using Microsoft.EntityFrameworkCore;
using Npgsql;
using server.Domain.Entities;
using server.Exceptions;
using server.Exceptions.Exceptions;

namespace server.ExceptionHandling;

/// <summary>
/// PostgresSQL-specific exception normalization for the API layer.
/// Keeps provider details out of <see cref="ApiExceptionHandler"/> by translating
/// known database constraint violations into domain-level <see cref="ServerException"/>s.
/// </summary>
internal static class PostgresExceptionHandler
{
    private const string PostgresUniqueViolationSqlState = "23505";
    private const string PostgresLockNotAvailableSqlState = "55P03";

    // Session/custody database guards (M9.6 Phase 2): trigger-raised check/exclusion violations. Deferred
    // date-coherence checks surface at commit as a bare PostgresException, so these map whatever wraps them.
    private static readonly Dictionary<string, string> SessionGuardMessages =
        new(StringComparer.Ordinal)
        {
            ["CK_DailyCloses_SessionIntervalOverlap"] = ResourcesErrorMessages.SESSION_DECLARE_OVERLAP,
            ["CK_DailyCloses_DeclaredWindow"] = ResourcesErrorMessages.SESSION_DECLARE_WINDOW_INVALID,
            ["CK_DailyCloses_SessionStartMatchesDate"] = ResourcesErrorMessages.SESSION_DECLARE_WINDOW_INVALID,
            ["CK_DailyCloses_ReadyHistoryRetained"] = ResourcesErrorMessages.SESSION_UNRESOLVED_NOT_WRITABLE,
            ["CK_DailyCloses_SessionAccountImmutable"] = ResourcesErrorMessages.SESSION_NOT_FOR_ACCOUNT,
            ["CK_Transactions_SessionDateCoherence"] = ResourcesErrorMessages.SESSION_DATE_COHERENCE,
            ["CK_Transactions_SessionImmutable"] = ResourcesErrorMessages.SESSION_NOT_FOR_ACCOUNT,
            ["CK_Transactions_CashboxRowRequiresSession"] = ResourcesErrorMessages.SESSION_OPENING_ACKNOWLEDGMENT_REQUIRED,
            ["CK_Transactions_SessionOnlyOnCashbox"] = ResourcesErrorMessages.SESSION_ONLY_ON_CASHBOX,
            ["FK_Transactions_DailyCloses_BranchId_AccountId_SessionId"] = ResourcesErrorMessages.SESSION_NOT_FOR_ACCOUNT,
            ["CK_SessionCountAttempts_Immutable"] = ResourcesErrorMessages.SESSION_CONFIRMATION_TOKEN_CONFLICT
        };

    private static readonly Dictionary<string, string> UniqueConstraintMessages =
        new(StringComparer.Ordinal)
        {
            ["IX_Users_Email"] = ResourcesErrorMessages.EMAIL_ALREADY_REGISTERED,
            ["IX_Invitations_BranchId_Email"] = ResourcesErrorMessages.INVITATION_PENDING_CONFLICT,
            ["IX_OperatorLinkRequests_BranchId_UserId"] = ResourcesErrorMessages.OPERATOR_REQUEST_CONFLICT,
            ["IX_Clients_BranchId_Cpf"] = ResourcesErrorMessages.CLIENT_CPF_CONFLICT,
            ["IX_Operators_BranchId_UserId"] = ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED,
            ["IX_DailyCloses_BranchId_AccountId_ActiveCustody"] = ResourcesErrorMessages.SESSION_ACTIVE_CUSTODY_CONFLICT,
            ["IX_DailyCloses_BranchId_AccountId_Sequence"] = ResourcesErrorMessages.SESSION_STALE_WRITE,
            // Conferir serializes on the cashbox key, so these are a backstop for a writer that bypassed it.
            ["IX_SessionCountAttempts_SessionId_ConfirmationToken"] = ResourcesErrorMessages.SESSION_CONFIRMATION_TOKEN_STALE,
            ["IX_SessionCountAttempts_SessionId_AttemptNumber"] = ResourcesErrorMessages.SESSION_CONFIRMATION_TOKEN_STALE,
            ["IX_SessionEvents_AttemptId_CountConferred"] = ResourcesErrorMessages.SESSION_CONFIRMATION_TOKEN_STALE,
            ["IX_SessionHandovers_OutgoingSessionId_Unresolved"] = ResourcesErrorMessages.SESSION_HANDOVER_PENDING,
            ["IX_SessionHandovers_IncomingSessionId"] = ResourcesErrorMessages.SESSION_HANDOVER_NOT_PENDING,
            ["IX_SessionOpenings_SessionId"] = ResourcesErrorMessages.SESSION_ACTIVE_CUSTODY_CONFLICT,
            ["IX_OpeningCounts_BranchId_AccountId_Current"] = ResourcesErrorMessages.OPENING_COUNT_STALE,
            ["IX_OpeningCounts_AccountId_Revision"] = ResourcesErrorMessages.OPENING_COUNT_STALE,
            ["IX_Terminals_BranchId_ExternalReference"] = ResourcesErrorMessages.TERMINAL_REFERENCE_CONFLICT,
            ["IX_DailyCloseItems_DailyCloseId_ProductId"] = ResourcesErrorMessages.DAILYCLOSE_ITEM_DUPLICATE,
            ["IX_TimeEntries_BranchId_OperatorId_Date"] = ResourcesErrorMessages.TIMEENTRY_DATE_CONFLICT,
            ["IX_TimeEntrySegments_TimeEntryId_OpenShift"] = ResourcesErrorMessages.TIMEENTRY_OPEN_SEGMENT_CONFLICT,
            ["IX_Holidays_BranchId_Date"] = ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT,
            ["IX_Categories_BranchId_Name"] = ResourcesErrorMessages.CATEGORY_NAME_CONFLICT,
            ["IX_TransactionTypes_CategoryId_Name"] = ResourcesErrorMessages.TRANSACTION_TYPE_NAME_CONFLICT,
            ["IX_Products_BranchId_Name"] = ResourcesErrorMessages.PRODUCT_NAME_CONFLICT,
            ["IX_IdempotencyRequests_Endpoint_BranchId_UserId_Key"] =
                ResourcesErrorMessages.IDEMPOTENCY_COORDINATION_BUSY,
            // Two same-day settings changes racing with the same If-Match version: the loser
            // either fails the Setting xmin update or, depending on statement order inside
            // its SaveChanges, this policy-ledger unique index — both mean a stale write.
            ["IX_TimeEntryPolicies_BranchId_EffectiveFrom"] = ResourcesErrorMessages.SETTING_STALE_WRITE
        };

    public static Exception Normalize(Exception exception)
    {
        var postgresException = FindPostgresException(exception);
        if (postgresException is { SqlState: PostgresLockNotAvailableSqlState })
            return new ConflictException(ResourcesErrorMessages.DAILYCLOSE_LEDGER_COORDINATION_BUSY);

        if (exception is not ServerException
            && postgresException is { ConstraintName: { } guardConstraint }
            && SessionGuardMessages.TryGetValue(guardConstraint, out var guardMessage))
        {
            return new ConflictException(guardMessage);
        }

        if (exception is DbUpdateConcurrencyException handoverConcurrencyException
            && handoverConcurrencyException.Entries.Any(entry => entry.Entity is SessionHandover))
        {
            return new ConflictException(ResourcesErrorMessages.SESSION_HANDOVER_STALE);
        }

        if (exception is DbUpdateConcurrencyException discrepancyConcurrencyException
            && discrepancyConcurrencyException.Entries.Any(entry => entry.Entity is SessionOpeningDiscrepancy))
        {
            return new ConflictException(ResourcesErrorMessages.OPENING_DISCREPANCY_STALE);
        }

        if (exception is DbUpdateConcurrencyException openingCountConcurrencyException
            && openingCountConcurrencyException.Entries.Any(entry => entry.Entity is OpeningCount))
        {
            return new ConflictException(ResourcesErrorMessages.OPENING_COUNT_STALE);
        }

        if (exception is DbUpdateConcurrencyException concurrencyException
            && concurrencyException.Entries.Any(entry => entry.Entity is DailyClose))
        {
            return new ConflictException(ResourcesErrorMessages.DAILYCLOSE_STALE_WRITE);
        }

        if (exception is DbUpdateConcurrencyException transactionConcurrencyException
            && transactionConcurrencyException.Entries.Any(entry => entry.Entity is Transaction))
        {
            return new ConflictException(ResourcesErrorMessages.TRANSACTION_STALE_WRITE);
        }

        if (exception is DbUpdateConcurrencyException settingConcurrencyException
            && settingConcurrencyException.Entries.Any(entry => entry.Entity is Setting))
        {
            return new ConflictException(ResourcesErrorMessages.SETTING_STALE_WRITE);
        }

        // Only rewrite known Postgres unique-violation cases. Everything else
        // flows through unchanged so the generic API exception handler can decide
        if (exception is ServerException || exception is not DbUpdateException || postgresException is not
            {
                SqlState: PostgresUniqueViolationSqlState,
                ConstraintName: not null
            })
        {
            return exception;
        }

        return UniqueConstraintMessages.TryGetValue(postgresException.ConstraintName, out var conflictMessage)
            ? new ConflictException(conflictMessage)
            : exception;
    }

    private static PostgresException? FindPostgresException(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgresException)
                return postgresException;
        }

        return null;
    }
}
