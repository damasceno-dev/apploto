namespace server.Infrastructure.Migrations;

/// <summary>
/// Database guards of the M9.6 Phase 4 count attempts (spec §3.30): an attempt belongs to a session of its own branch,
/// and an attempt and its items are immutable — no update or delete, from any writer.
/// </summary>
internal static class SessionCountAttemptGuardsSql
{
    public const string Install =
        """
        ALTER TABLE "DailyCloses" ADD CONSTRAINT "AK_DailyCloses_BranchId_Id" UNIQUE ("BranchId", "Id");
        ALTER TABLE "SessionCountAttempts" ADD CONSTRAINT "FK_SessionCountAttempts_DailyCloses_BranchId_SessionId"
            FOREIGN KEY ("BranchId", "SessionId") REFERENCES "DailyCloses" ("BranchId", "Id") ON DELETE RESTRICT;

        CREATE FUNCTION session_count_attempt_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION 'a count attempt is immutable'
                USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SessionCountAttempts_Immutable';
        END;
        $$;

        CREATE TRIGGER "TR_SessionCountAttempts_Immutable"
            BEFORE UPDATE OR DELETE ON "SessionCountAttempts"
            FOR EACH ROW EXECUTE FUNCTION session_count_attempt_immutable();
        CREATE TRIGGER "TR_SessionCountAttemptItems_Immutable"
            BEFORE UPDATE OR DELETE ON "SessionCountAttemptItems"
            FOR EACH ROW EXECUTE FUNCTION session_count_attempt_immutable();
        """;

    public const string Uninstall =
        """
        DROP TRIGGER IF EXISTS "TR_SessionCountAttemptItems_Immutable" ON "SessionCountAttemptItems";
        DROP TRIGGER IF EXISTS "TR_SessionCountAttempts_Immutable" ON "SessionCountAttempts";
        DROP FUNCTION IF EXISTS session_count_attempt_immutable();
        ALTER TABLE "SessionCountAttempts" DROP CONSTRAINT IF EXISTS "FK_SessionCountAttempts_DailyCloses_BranchId_SessionId";
        ALTER TABLE "DailyCloses" DROP CONSTRAINT IF EXISTS "AK_DailyCloses_BranchId_Id";
        """;
}
