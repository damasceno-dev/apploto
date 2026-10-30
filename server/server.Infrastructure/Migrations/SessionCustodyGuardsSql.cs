namespace server.Infrastructure.Migrations;

/// <summary>
/// Database guards of the M9.6 Phase 2 session/custody model (spec §3.30). They hold even against writers that
/// bypass the application: non-overlapping Ready custody intervals per cashbox serialized on the Account row,
/// retained Ready history, start/date and declared-window coherence, cashbox rows bound to a session of the same
/// branch and cashbox, an immutable session link and deferred row/session business-date coherence.
/// </summary>
internal static class SessionCustodyGuardsSql
{
    // Branch business dates are America/Sao_Paulo (BranchClock).
    public const string Install =
        """
        ALTER TABLE "DailyCloses" ADD CONSTRAINT "AK_DailyCloses_BranchId_AccountId_Id" UNIQUE ("BranchId", "AccountId", "Id");
        ALTER TABLE "Transactions" ADD CONSTRAINT "FK_Transactions_DailyCloses_BranchId_AccountId_SessionId"
            FOREIGN KEY ("BranchId", "AccountId", "SessionId") REFERENCES "DailyCloses" ("BranchId", "AccountId", "Id")
            ON DELETE RESTRICT;

        ALTER TABLE "Operators" ADD CONSTRAINT "AK_Operators_BranchId_Id" UNIQUE ("BranchId", "Id");
        ALTER TABLE "Terminals" ADD CONSTRAINT "AK_Terminals_BranchId_Id" UNIQUE ("BranchId", "Id");
        ALTER TABLE "DailyCloses" ADD CONSTRAINT "FK_DailyCloses_Operators_BranchId_CustodianOperatorId"
            FOREIGN KEY ("BranchId", "CustodianOperatorId") REFERENCES "Operators" ("BranchId", "Id") ON DELETE RESTRICT;
        ALTER TABLE "DailyCloses" ADD CONSTRAINT "FK_DailyCloses_Terminals_BranchId_TerminalId"
            FOREIGN KEY ("BranchId", "TerminalId") REFERENCES "Terminals" ("BranchId", "Id") ON DELETE RESTRICT;
        ALTER TABLE "Accounts" ADD CONSTRAINT "FK_Accounts_Terminals_BranchId_TerminalId"
            FOREIGN KEY ("BranchId", "TerminalId") REFERENCES "Terminals" ("BranchId", "Id") ON DELETE RESTRICT;

        CREATE INDEX "IX_DailyCloses_BranchId_AccountId_Interval" ON "DailyCloses" ("BranchId", "AccountId", "StartedAt")
            WHERE "MappingState" = 0;

        CREATE FUNCTION session_custody_guard() RETURNS trigger LANGUAGE plpgsql VOLATILE AS $$
        BEGIN
            IF TG_OP = 'UPDATE' THEN
                IF NEW."AccountId" <> OLD."AccountId" THEN
                    RAISE EXCEPTION 'session cashbox is immutable'
                        USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_DailyCloses_SessionAccountImmutable';
                END IF;
                IF OLD."MappingState" = 0 AND (NEW."MappingState" <> 0 OR (OLD."Active" AND NOT NEW."Active")) THEN
                    RAISE EXCEPTION 'ready custody history cannot be downgraded or deactivated'
                        USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_DailyCloses_ReadyHistoryRetained';
                END IF;
            END IF;

            IF NEW."MappingState" = 0 THEN
                IF (NEW."StartedAt" AT TIME ZONE 'America/Sao_Paulo')::date <> NEW."Date" THEN
                    RAISE EXCEPTION 'session start does not fall on its business date'
                        USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_DailyCloses_SessionStartMatchesDate';
                END IF;
                IF NEW."Origin" = 3 AND (NEW."EndedAt" IS NULL
                        OR NEW."EndedAt" > now()
                        OR NEW."EndedAt" > ((NEW."Date" + 1)::timestamp AT TIME ZONE 'America/Sao_Paulo')) THEN
                    RAISE EXCEPTION 'declared session window must be past and inside one business day'
                        USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_DailyCloses_DeclaredWindow';
                END IF;

                -- Serialize competing intervals on the cashbox root row, then check committed history.
                UPDATE "Accounts" SET "CustodyHistoryRevision" = "CustodyHistoryRevision" + 1 WHERE "Id" = NEW."AccountId";
                IF EXISTS (
                    SELECT 1 FROM "DailyCloses" d
                    WHERE d."BranchId" = NEW."BranchId"
                      AND d."AccountId" = NEW."AccountId"
                      AND d."Id" <> NEW."Id"
                      AND d."MappingState" = 0
                      AND tstzrange(d."StartedAt", d."EndedAt", '[)') && tstzrange(NEW."StartedAt", NEW."EndedAt", '[)')) THEN
                    RAISE EXCEPTION 'session custody intervals overlap'
                        USING ERRCODE = 'exclusion_violation', CONSTRAINT = 'CK_DailyCloses_SessionIntervalOverlap';
                END IF;
            END IF;

            RETURN NEW;
        END $$;

        CREATE TRIGGER "TR_DailyCloses_SessionCustodyGuard"
            BEFORE INSERT OR UPDATE OF "MappingState", "Active", "AccountId", "StartedAt", "EndedAt", "Date", "Origin"
            ON "DailyCloses" FOR EACH ROW EXECUTE FUNCTION session_custody_guard();

        CREATE FUNCTION session_history_delete_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF OLD."MappingState" = 0 THEN
                RAISE EXCEPTION 'ready custody history cannot be deleted'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_DailyCloses_ReadyHistoryRetained';
            END IF;
            RETURN OLD;
        END $$;

        CREATE TRIGGER "TR_DailyCloses_SessionHistoryDeleteGuard"
            BEFORE DELETE ON "DailyCloses" FOR EACH ROW EXECUTE FUNCTION session_history_delete_guard();

        CREATE FUNCTION transaction_session_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE account_type integer;
        BEGIN
            IF TG_OP = 'UPDATE' AND OLD."SessionId" IS NOT NULL AND NEW."SessionId" IS DISTINCT FROM OLD."SessionId" THEN
                RAISE EXCEPTION 'a row never moves to another session'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_Transactions_SessionImmutable';
            END IF;

            SELECT "Type" INTO account_type FROM "Accounts" WHERE "Id" = NEW."AccountId";
            IF TG_OP = 'INSERT' AND account_type = 0 AND NEW."SessionId" IS NULL THEN
                RAISE EXCEPTION 'a cashbox row requires its session'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_Transactions_CashboxRowRequiresSession';
            END IF;
            IF account_type <> 0 AND NEW."SessionId" IS NOT NULL THEN
                RAISE EXCEPTION 'only cashbox rows carry a session'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_Transactions_SessionOnlyOnCashbox';
            END IF;

            RETURN NEW;
        END $$;

        CREATE TRIGGER "TR_Transactions_SessionGuard"
            BEFORE INSERT OR UPDATE OF "SessionId" ON "Transactions"
            FOR EACH ROW EXECUTE FUNCTION transaction_session_guard();

        CREATE FUNCTION transaction_session_date_coherence() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."SessionId" IS NOT NULL AND EXISTS (
                SELECT 1 FROM "DailyCloses" d WHERE d."Id" = NEW."SessionId" AND d."Date" <> NEW."Date") THEN
                RAISE EXCEPTION 'a cashbox row must carry its session business date'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_Transactions_SessionDateCoherence';
            END IF;
            RETURN NULL;
        END $$;

        CREATE CONSTRAINT TRIGGER "TR_Transactions_SessionDateCoherence"
            AFTER INSERT OR UPDATE OF "Date", "SessionId" ON "Transactions"
            DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION transaction_session_date_coherence();

        CREATE FUNCTION session_row_date_coherence() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."Date" <> OLD."Date" AND EXISTS (
                SELECT 1 FROM "Transactions" t WHERE t."SessionId" = NEW."Id" AND t."Date" <> NEW."Date") THEN
                RAISE EXCEPTION 'a cashbox row must carry its session business date'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_Transactions_SessionDateCoherence';
            END IF;
            RETURN NULL;
        END $$;

        CREATE CONSTRAINT TRIGGER "TR_DailyCloses_SessionRowDateCoherence"
            AFTER UPDATE OF "Date" ON "DailyCloses"
            DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION session_row_date_coherence();
        """;

    public const string Uninstall =
        """
        DROP TRIGGER IF EXISTS "TR_DailyCloses_SessionRowDateCoherence" ON "DailyCloses";
        DROP FUNCTION IF EXISTS session_row_date_coherence();
        DROP TRIGGER IF EXISTS "TR_Transactions_SessionDateCoherence" ON "Transactions";
        DROP FUNCTION IF EXISTS transaction_session_date_coherence();
        DROP TRIGGER IF EXISTS "TR_Transactions_SessionGuard" ON "Transactions";
        DROP FUNCTION IF EXISTS transaction_session_guard();
        DROP TRIGGER IF EXISTS "TR_DailyCloses_SessionHistoryDeleteGuard" ON "DailyCloses";
        DROP FUNCTION IF EXISTS session_history_delete_guard();
        DROP TRIGGER IF EXISTS "TR_DailyCloses_SessionCustodyGuard" ON "DailyCloses";
        DROP FUNCTION IF EXISTS session_custody_guard();
        DROP INDEX IF EXISTS "IX_DailyCloses_BranchId_AccountId_Interval";
        ALTER TABLE "Accounts" DROP CONSTRAINT IF EXISTS "FK_Accounts_Terminals_BranchId_TerminalId";
        ALTER TABLE "DailyCloses" DROP CONSTRAINT IF EXISTS "FK_DailyCloses_Terminals_BranchId_TerminalId";
        ALTER TABLE "DailyCloses" DROP CONSTRAINT IF EXISTS "FK_DailyCloses_Operators_BranchId_CustodianOperatorId";
        ALTER TABLE "Terminals" DROP CONSTRAINT IF EXISTS "AK_Terminals_BranchId_Id";
        ALTER TABLE "Operators" DROP CONSTRAINT IF EXISTS "AK_Operators_BranchId_Id";
        ALTER TABLE "Transactions" DROP CONSTRAINT IF EXISTS "FK_Transactions_DailyCloses_BranchId_AccountId_SessionId";
        ALTER TABLE "DailyCloses" DROP CONSTRAINT IF EXISTS "AK_DailyCloses_BranchId_AccountId_Id";
        """;
}
