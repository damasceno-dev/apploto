namespace server.Infrastructure.Migrations;

/// <summary>
/// M9.7 Phases 1–2: the upgrade steps (system type, frozen-classification backfill, legacy <c>PaidAt</c> reconstruction)
/// and the database guards for settlement events, bank receipts and frozen classification. Kept apart from the
/// migration so the persistence tests can replay the reconstruction against prior-schema rows.
/// </summary>
internal static class SettlementEventsSql
{
    /// <summary>System types carry the class fixed by their role; every branch gets the cheque-conversion debt type.</summary>
    public const string SystemTypes =
        """
        UPDATE "TransactionTypes"
        SET "FinancialClass" = CASE "SystemRole"
            WHEN 1 THEN 1 WHEN 2 THEN 1 WHEN 3 THEN 1 WHEN 4 THEN 1 WHEN 5 THEN 2 WHEN 6 THEN 2 WHEN 7 THEN 4
            WHEN 8 THEN 4 WHEN 9 THEN 1 END
        WHERE "SystemRole" IS NOT NULL;

        INSERT INTO "TransactionTypes" (
            "Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId", "Active", "CreatedAt",
            "SystemRole", "SettlementInstrument", "MonthlyFinancialRole", "FinancialClass")
        SELECT gen_random_uuid(), 'Cheque convertido em fiado', 0, true, sale."CategoryId", true, now(),
               9, 0, 0, 1
        FROM "TransactionTypes" sale
        INNER JOIN "Categories" c ON c."Id" = sale."CategoryId"
        WHERE sale."SystemRole" = 1
          AND NOT EXISTS (
              SELECT 1 FROM "TransactionTypes" existing
              INNER JOIN "Categories" ec ON ec."Id" = existing."CategoryId"
              WHERE existing."SystemRole" = 9 AND ec."BranchId" = c."BranchId");
        """;

    /// <summary>
    /// Every posted (Active) row gets a snapshot copied from its type at the upgrade, marked
    /// <c>MigratedFromTypeAtUpgrade</c>: it equals what the query-time reports showed, not proof of the historical
    /// classification. Draft rows stay unposted; cancelled rows keep no snapshot (their posting history is unknown).
    /// </summary>
    public const string BackfillClassification =
        """
        UPDATE "Transactions" t
        SET "PostedSettlementInstrument" = tt."SettlementInstrument",
            "PostedMonthlyFinancialRole" = tt."MonthlyFinancialRole",
            "PostedFinancialClass" = tt."FinancialClass",
            "ClassificationProvenance" = 2,
            "ClassificationRevision" = 1,
            "ClassifiedAt" = now()
        FROM "TransactionTypes" tt
        WHERE tt."Id" = t."TransactionTypeId" AND t."Status" = 1 AND t."ClassificationRevision" = 0;
        """;

    /// <summary>
    /// Each legacy <c>PaidAt</c> becomes one reconstructed event with <c>OccurredOn = PostedOn</c> = its branch-local
    /// date and the original instant kept. Never a new payment: no receipt, bank row or ledger row. A Tab row's
    /// <c>PaidAt</c> never settled fiado, so its event changes nothing; anomalies are flagged, never corrected. Re-running
    /// adds nothing (one reconstructed event per row).
    /// </summary>
    public const string ReconstructPaidAt =
        """
        INSERT INTO "SettlementEvents" (
            "Id", "BranchId", "ReceivableTransactionId", "Sequence", "PreviousEventId", "Kind", "Amount",
            "OutstandingDelta", "OccurredOn", "PostedOn", "Provenance", "MigrationIssue", "SourcePaidAt",
            "SettlementInstrument", "CreatedAt", "Active")
        SELECT gen_random_uuid(), t."BranchId", t."Id", 1, NULL, 1, t."Value",
               CASE WHEN a."Type" = 2 THEN 0 ELSE -t."Value" END,
               d.local_date, d.local_date, 2,
               ((CASE WHEN a."Type" = 2 THEN 1 ELSE 0 END)
                 | (CASE WHEN d.local_date < t."Date" THEN 2 ELSE 0 END)
                 | (CASE WHEN t."Status" = 0 THEN 4 ELSE 0 END)
                 | (CASE WHEN t."Status" = 2 THEN 8 ELSE 0 END))::smallint,
               t."PaidAt",
               COALESCE(t."PostedSettlementInstrument", tt."SettlementInstrument"),
               now(), true
        FROM "Transactions" t
        INNER JOIN "Accounts" a ON a."Id" = t."AccountId"
        INNER JOIN "TransactionTypes" tt ON tt."Id" = t."TransactionTypeId"
        CROSS JOIN LATERAL (SELECT (t."PaidAt" AT TIME ZONE 'America/Sao_Paulo')::date AS local_date) d
        WHERE t."PaidAt" IS NOT NULL
          AND NOT EXISTS (
              SELECT 1 FROM "SettlementEvents" e
              WHERE e."ReceivableTransactionId" = t."Id" AND e."Provenance" = 2);
        """;

    public const string InstallGuards =
        """
        CREATE FUNCTION transaction_type_system_class() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."SystemRole" IS NOT NULL THEN
                NEW."FinancialClass" := CASE NEW."SystemRole"
                    WHEN 1 THEN 1 WHEN 2 THEN 1 WHEN 3 THEN 1 WHEN 4 THEN 1 WHEN 5 THEN 2 WHEN 6 THEN 2 WHEN 7 THEN 4
                    WHEN 8 THEN 4 WHEN 9 THEN 1 END;
            END IF;
            RETURN NEW;
        END $$;

        CREATE TRIGGER "TR_TransactionTypes_SystemClass"
            BEFORE INSERT OR UPDATE ON "TransactionTypes"
            FOR EACH ROW EXECUTE FUNCTION transaction_type_system_class();

        CREATE FUNCTION transaction_classification_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE type_row record;
        BEGIN
            IF TG_OP = 'INSERT' OR OLD."ClassificationRevision" = 0 THEN
                IF NEW."Status" = 1 THEN
                    SELECT "SettlementInstrument", "MonthlyFinancialRole", "FinancialClass" INTO type_row
                    FROM "TransactionTypes" WHERE "Id" = NEW."TransactionTypeId";
                    NEW."PostedSettlementInstrument" := type_row."SettlementInstrument";
                    NEW."PostedMonthlyFinancialRole" := type_row."MonthlyFinancialRole";
                    NEW."PostedFinancialClass" := type_row."FinancialClass";
                    NEW."ClassificationProvenance" := 1;
                    NEW."ClassificationRevision" := 1;
                    NEW."ClassifiedAt" := now();
                ELSE
                    NEW."PostedSettlementInstrument" := NULL;
                    NEW."PostedMonthlyFinancialRole" := NULL;
                    NEW."PostedFinancialClass" := NULL;
                    NEW."ClassificationProvenance" := NULL;
                    NEW."ClassificationRevision" := 0;
                    NEW."ClassifiedAt" := NULL;
                END IF;
                RETURN NEW;
            END IF;

            IF ROW(NEW."PostedSettlementInstrument", NEW."PostedMonthlyFinancialRole", NEW."PostedFinancialClass",
                   NEW."ClassificationProvenance", NEW."ClassificationRevision", NEW."ClassifiedAt")
               IS DISTINCT FROM
               ROW(OLD."PostedSettlementInstrument", OLD."PostedMonthlyFinancialRole", OLD."PostedFinancialClass",
                   OLD."ClassificationProvenance", OLD."ClassificationRevision", OLD."ClassifiedAt") THEN
                -- A receivable with a settlement lifecycle keeps the instrument its events were recorded under.
                IF NEW."PostedSettlementInstrument" IS DISTINCT FROM OLD."PostedSettlementInstrument"
                   AND EXISTS (
                       SELECT 1 FROM "SettlementEvents" e
                       WHERE e."ReceivableTransactionId" = NEW."Id" AND e."Provenance" = 1) THEN
                    RAISE EXCEPTION 'a receivable with settlement events keeps its instrument'
                        USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_Transactions_ClassificationFrozen';
                END IF;
                IF NEW."ClassificationRevision" = OLD."ClassificationRevision" + 1
                   AND NEW."ClassificationProvenance" = 3
                   AND EXISTS (
                       SELECT 1 FROM "TransactionClassificationRevisions" r
                       WHERE r."TransactionId" = NEW."Id" AND r."Revision" = NEW."ClassificationRevision"
                         AND r."SettlementInstrument" = NEW."PostedSettlementInstrument"
                         AND r."MonthlyFinancialRole" = NEW."PostedMonthlyFinancialRole"
                         AND r."FinancialClass" = NEW."PostedFinancialClass") THEN
                    RETURN NEW;
                END IF;
                RAISE EXCEPTION 'a posted row keeps its classification snapshot'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_Transactions_ClassificationFrozen';
            END IF;
            RETURN NEW;
        END $$;

        CREATE TRIGGER "TR_Transactions_Classification"
            BEFORE INSERT OR UPDATE ON "Transactions"
            FOR EACH ROW EXECUTE FUNCTION transaction_classification_guard();
        -- Posting defines the snapshot, so it is captured even by writes that bypass the ordinary row guards.
        ALTER TABLE "Transactions" ENABLE ALWAYS TRIGGER "TR_Transactions_Classification";
        ALTER TABLE "TransactionTypes" ENABLE ALWAYS TRIGGER "TR_TransactionTypes_SystemClass";

        CREATE TRIGGER "TR_SettlementEvents_Immutable"
            BEFORE UPDATE OR DELETE ON "SettlementEvents"
            FOR EACH ROW EXECUTE FUNCTION financial_history_immutable();

        CREATE TRIGGER "TR_TransactionClassificationRevisions_Immutable"
            BEFORE UPDATE OR DELETE ON "TransactionClassificationRevisions"
            FOR EACH ROW EXECUTE FUNCTION financial_history_immutable();

        CREATE FUNCTION settlement_event_chain_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE receivable record; previous record; reversed record; total numeric;
        BEGIN
            SELECT "BranchId", "Value", "Date" INTO receivable FROM "Transactions" WHERE "Id" = NEW."ReceivableTransactionId";
            IF NOT FOUND OR receivable."BranchId" IS DISTINCT FROM NEW."BranchId" THEN
                RAISE EXCEPTION 'an event belongs to a receivable of its branch'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SettlementEvents_ChainCoherent';
            END IF;

            IF NEW."Sequence" > 1 THEN
                SELECT * INTO previous FROM "SettlementEvents" WHERE "Id" = NEW."PreviousEventId";
                IF NOT FOUND OR previous."ReceivableTransactionId" <> NEW."ReceivableTransactionId"
                   OR previous."Sequence" <> NEW."Sequence" - 1 THEN
                    RAISE EXCEPTION 'an event follows its receivable''s previous event'
                        USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SettlementEvents_ChainCoherent';
                END IF;
                IF NEW."OccurredOn" < previous."OccurredOn" OR NEW."PostedOn" < previous."PostedOn" THEN
                    RAISE EXCEPTION 'an event never precedes its predecessor on either date'
                        USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SettlementEvents_Chronology';
                END IF;
            END IF;

            -- The first command event records the row's frozen instrument; later events keep the chain's.
            IF NEW."Provenance" = 1 AND NOT EXISTS (
                   SELECT 1 FROM "SettlementEvents" e
                   WHERE e."ReceivableTransactionId" = NEW."ReceivableTransactionId" AND e."Provenance" = 1)
               AND NEW."SettlementInstrument" IS DISTINCT FROM (
                   SELECT COALESCE(t."PostedSettlementInstrument", 0) FROM "Transactions" t
                   WHERE t."Id" = NEW."ReceivableTransactionId") THEN
                RAISE EXCEPTION 'the first command event records the receivable''s frozen instrument'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SettlementEvents_ChainCoherent';
            END IF;

            IF NEW."Provenance" = 1 AND NEW."OccurredOn" < receivable."Date" THEN
                RAISE EXCEPTION 'an event never precedes its receivable'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SettlementEvents_Chronology';
            END IF;

            SELECT COALESCE(SUM("OutstandingDelta"), 0) INTO total
            FROM "SettlementEvents" WHERE "ReceivableTransactionId" = NEW."ReceivableTransactionId";
            IF receivable."Value" + total + NEW."OutstandingDelta" < 0
               OR total + NEW."OutstandingDelta" > 0 THEN
                RAISE EXCEPTION 'the outstanding amount stays between zero and the face value'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SettlementEvents_OutstandingWithinValue';
            END IF;

            IF NEW."Kind" = 4 THEN
                SELECT * INTO reversed FROM "SettlementEvents" WHERE "Id" = NEW."ReversesEventId";
                IF NOT FOUND OR reversed."Kind" <> 3 OR reversed."ReceivableTransactionId" <> NEW."ReceivableTransactionId"
                   OR reversed."Amount" <> NEW."Amount" THEN
                    RAISE EXCEPTION 'a return reverses a deposit of the same receivable and amount'
                        USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SettlementEvents_ChainCoherent';
                END IF;
            END IF;

            IF NEW."BankReceiptId" IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM "BankReceipts" WHERE "Id" = NEW."BankReceiptId" AND "BranchId" = NEW."BranchId") THEN
                RAISE EXCEPTION 'a deposit claims a receipt of its branch'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SettlementEvents_ChainCoherent';
            END IF;

            IF NEW."ConvertedTransactionId" IS NOT NULL AND NOT EXISTS (
                SELECT 1 FROM "Transactions" debt
                INNER JOIN "Accounts" a ON a."Id" = debt."AccountId"
                WHERE debt."Id" = NEW."ConvertedTransactionId" AND debt."BranchId" = NEW."BranchId"
                  AND debt."GeneratedFromTransactionId" = NEW."ReceivableTransactionId" AND debt."GenerationKind" = 7
                  AND debt."Value" = NEW."Amount" AND debt."ClientId" = NEW."ClientId" AND a."Type" = 2
                  AND debt."Direction" = 1 AND debt."Status" = 1) THEN
                RAISE EXCEPTION 'a conversion opens the same client''s Tab debt of the same amount'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SettlementEvents_ChainCoherent';
            END IF;

            RETURN NEW;
        END $$;

        CREATE TRIGGER "TR_SettlementEvents_ChainGuard"
            BEFORE INSERT ON "SettlementEvents"
            FOR EACH ROW EXECUTE FUNCTION settlement_event_chain_guard();

        CREATE FUNCTION bank_receipt_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF TG_OP = 'DELETE' OR ROW(NEW."Id", NEW."BranchId", NEW."BankAccountId", NEW."SourceKind", NEW."OccurredOn",
                    NEW."PostedOn", NEW."Amount", NEW."Description", NEW."CreatedByUserId", NEW."CreatedAt")
                IS DISTINCT FROM ROW(OLD."Id", OLD."BranchId", OLD."BankAccountId", OLD."SourceKind", OLD."OccurredOn",
                    OLD."PostedOn", OLD."Amount", OLD."Description", OLD."CreatedByUserId", OLD."CreatedAt") THEN
                RAISE EXCEPTION 'a bank receipt changes only its status'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankReceipts_Immutable';
            END IF;
            RETURN NEW;
        END $$;

        CREATE TRIGGER "TR_BankReceipts_Immutable"
            BEFORE UPDATE OR DELETE ON "BankReceipts"
            FOR EACH ROW EXECUTE FUNCTION bank_receipt_immutable();

        CREATE FUNCTION bank_receipt_allocations_coherent() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NOT EXISTS (
                SELECT 1 FROM "Accounts" a
                WHERE a."Id" = NEW."BankAccountId" AND a."BranchId" = NEW."BranchId" AND a."Type" = 1)
               OR NEW."Amount" IS DISTINCT FROM (
                SELECT SUM(e."Amount") FROM "SettlementEvents" e WHERE e."BankReceiptId" = NEW."Id" AND e."Kind" = 3) THEN
                RAISE EXCEPTION 'a receipt is a bank account''s and equals the sum of its deposits'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankReceipts_AllocationsCoherent';
            END IF;
            RETURN NULL;
        END $$;

        CREATE CONSTRAINT TRIGGER "TR_BankReceipts_AllocationsCoherent"
            AFTER INSERT ON "BankReceipts"
            DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION bank_receipt_allocations_coherent();
        """;

    public const string UninstallGuards =
        """
        DROP TRIGGER IF EXISTS "TR_BankReceipts_AllocationsCoherent" ON "BankReceipts";
        DROP FUNCTION IF EXISTS bank_receipt_allocations_coherent();
        DROP TRIGGER IF EXISTS "TR_BankReceipts_Immutable" ON "BankReceipts";
        DROP FUNCTION IF EXISTS bank_receipt_immutable();
        DROP TRIGGER IF EXISTS "TR_SettlementEvents_ChainGuard" ON "SettlementEvents";
        DROP FUNCTION IF EXISTS settlement_event_chain_guard();
        DROP TRIGGER IF EXISTS "TR_TransactionClassificationRevisions_Immutable" ON "TransactionClassificationRevisions";
        DROP TRIGGER IF EXISTS "TR_SettlementEvents_Immutable" ON "SettlementEvents";
        DROP TRIGGER IF EXISTS "TR_Transactions_Classification" ON "Transactions";
        DROP FUNCTION IF EXISTS transaction_classification_guard();
        DROP TRIGGER IF EXISTS "TR_TransactionTypes_SystemClass" ON "TransactionTypes";
        DROP FUNCTION IF EXISTS transaction_type_system_class();
        """;

    /// <summary>
    /// The supported downgrade is only the one that loses nothing: before any post-upgrade financial history exists.
    /// Otherwise it stops and changes nothing; reconstructed events are folded back into <c>PaidAt</c>.
    /// </summary>
    public const string DowngradePreflight =
        """
        DO $$
        BEGIN
            IF EXISTS (SELECT 1 FROM "SettlementEvents" WHERE "Provenance" <> 2)
               OR EXISTS (SELECT 1 FROM "BankReceipts")
               OR EXISTS (SELECT 1 FROM "TransactionClassificationRevisions")
               OR EXISTS (SELECT 1 FROM "Transactions" WHERE "ClassificationProvenance" IN (1, 3) OR "GenerationKind" = 7) THEN
                RAISE EXCEPTION 'M97_DOWNGRADE: settlement events, bank receipts, reclassifications or rows posted after the M9.7 upgrade exist; a downgrade would erase that history';
            END IF;
        END $$;
        """;

    public const string RestorePaidAt =
        """
        UPDATE "Transactions" t
        SET "PaidAt" = e."SourcePaidAt"
        FROM "SettlementEvents" e
        WHERE e."ReceivableTransactionId" = t."Id" AND e."Provenance" = 2;

        DELETE FROM "TransactionTypes" WHERE "SystemRole" = 9;
        """;
}
