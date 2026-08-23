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

    private static readonly Dictionary<string, string> UniqueConstraintMessages =
        new(StringComparer.Ordinal)
        {
            ["IX_Clients_BranchId_Cpf"] = ResourcesErrorMessages.CLIENT_CPF_CONFLICT,
            ["IX_Operators_BranchId_UserId"] = ResourcesErrorMessages.OPERATOR_USER_ALREADY_LINKED,
            ["IX_DailyCloses_BranchId_AccountId_Date"] = ResourcesErrorMessages.DAILYCLOSE_DATE_CONFLICT,
            ["IX_DailyCloseItems_DailyCloseId_ProductId"] = ResourcesErrorMessages.DAILYCLOSE_ITEM_DUPLICATE,
            ["IX_TimeEntries_BranchId_OperatorId_Date"] = ResourcesErrorMessages.TIMEENTRY_DATE_CONFLICT,
            ["IX_TimeEntrySegments_TimeEntryId_OpenShift"] = ResourcesErrorMessages.TIMEENTRY_OPEN_SEGMENT_CONFLICT,
            ["IX_Holidays_BranchId_Date"] = ResourcesErrorMessages.HOLIDAY_DATE_CONFLICT,
            ["IX_Categories_BranchId_Name"] = ResourcesErrorMessages.CATEGORY_NAME_CONFLICT,
            ["IX_TransactionTypes_CategoryId_Name"] = ResourcesErrorMessages.TRANSACTION_TYPE_NAME_CONFLICT,
            ["IX_Products_BranchId_Name"] = ResourcesErrorMessages.PRODUCT_NAME_CONFLICT,
            ["IX_IdempotencyRequests_Endpoint_BranchId_UserId_Key"] =
                ResourcesErrorMessages.IDEMPOTENCY_COORDINATION_BUSY
        };

    public static Exception Normalize(Exception exception)
    {
        var postgresException = FindPostgresException(exception);
        if (postgresException is { SqlState: PostgresLockNotAvailableSqlState })
            return new ConflictException(ResourcesErrorMessages.DAILYCLOSE_LEDGER_COORDINATION_BUSY);

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
