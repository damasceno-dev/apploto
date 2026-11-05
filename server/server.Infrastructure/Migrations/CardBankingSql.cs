namespace server.Infrastructure.Migrations;

internal static class CardBankingSql
{
    public const string Install = """
        CREATE OR REPLACE FUNCTION bank_receipt_allocations_coherent() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE expected numeric; batch record;
        BEGIN
            IF NEW."Status" <> 1 THEN
                RAISE EXCEPTION 'new receipts await statement confirmation' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankReceipts_Confirmation';
            END IF;
            IF NEW."SourceKind" = 1 THEN
                SELECT SUM(e."Amount") INTO expected FROM "SettlementEvents" e WHERE e."BankReceiptId" = NEW."Id" AND e."Kind" = 3;
            ELSIF NEW."SourceKind" = 2 THEN
                SELECT * INTO batch FROM "CardSettlementBatches" WHERE "BankReceiptId" = NEW."Id";
                IF NOT FOUND OR batch."BranchId" <> NEW."BranchId" OR NOT EXISTS (
                    SELECT 1 FROM "CardProviders" p WHERE p."Id" = batch."ProviderId" AND p."BranchId" = NEW."BranchId" AND p."BankAccountId" = NEW."BankAccountId") OR batch."GrossAmount" IS DISTINCT FROM (
                    SELECT SUM(e."Amount") FROM "SettlementEvents" e WHERE e."BankReceiptId" = NEW."Id" AND e."Kind" = 7) THEN
                    RAISE EXCEPTION 'card batch gross allocations disagree' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankReceipts_AllocationsCoherent';
                END IF;
                expected := batch."GrossAmount" - COALESCE(batch."ConfirmedFee", 0);
            ELSE
                RAISE EXCEPTION 'unsupported bank receipt source' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankReceipts_AllocationsCoherent';
            END IF;
            IF expected IS NULL OR NEW."Amount" IS DISTINCT FROM expected OR NOT EXISTS (
                SELECT 1 FROM "Accounts" a WHERE a."Id" = NEW."BankAccountId" AND a."BranchId" = NEW."BranchId" AND a."Type" = 1) THEN
                RAISE EXCEPTION 'bank receipt allocations disagree' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankReceipts_AllocationsCoherent';
            END IF;
            RETURN NULL;
        END $$;

        CREATE FUNCTION bank_match_coherent() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE p record; l record; r record; expected numeric;
        BEGIN
            SELECT * INTO p FROM "BankMatchProposals" WHERE "Id" = NEW."ProposalId";
            SELECT * INTO l FROM "BankStatementLines" WHERE "Id" = NEW."LineId";
            SELECT * INTO r FROM "BankReceipts" WHERE "Id" = NEW."ReceiptId";
            SELECT r."Amount" - COALESCE(SUM(e."Amount"), 0) INTO expected
            FROM "SettlementEvents" e INNER JOIN "SettlementEvents" d ON d."Id" = e."ReversesEventId"
            WHERE d."BankReceiptId" = r."Id" AND e."Kind" = 4;
            IF r."SourceKind" IN (5, 6) THEN expected := -expected; END IF;
            IF p."LineId" IS DISTINCT FROM l."Id" OR p."ReceiptId" IS DISTINCT FROM r."Id"
               OR p."BranchId" IS DISTINCT FROM NEW."BranchId" OR l."BranchId" IS DISTINCT FROM NEW."BranchId"
               OR r."BranchId" IS DISTINCT FROM NEW."BranchId" OR l."BankAccountId" IS DISTINCT FROM r."BankAccountId"
               OR p."BankAccountId" IS DISTINCT FROM r."BankAccountId" OR r."Status" <> 2 OR l."AmbiguousIdentity"
               OR NEW."Amount" IS DISTINCT FROM expected OR NEW."Amount" IS DISTINCT FROM l."Amount"
               OR NEW."Amount" IS DISTINCT FROM p."Amount" OR NEW."PostedOn" < r."PostedOn" OR NEW."PostedOn" < l."PostedOn"
               OR EXISTS (SELECT 1 FROM "CardSettlementBatches" b WHERE b."BankReceiptId" = r."Id" AND (b."ConfirmedFee" IS NULL OR (b."ObservedNetAmount" IS NOT NULL AND b."ObservedNetAmount" <> r."Amount"))) THEN
                RAISE EXCEPTION 'bank match evidence disagrees' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankStatementMatches_Coherent';
            END IF;
            RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER "TR_BankStatementMatches_Coherent" AFTER INSERT ON "BankStatementMatches"
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION bank_match_coherent();

        CREATE FUNCTION bank_receipt_confirmation_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."Status" <> 2 OR (TG_OP = 'UPDATE' AND OLD."Status" <> 1) OR NOT EXISTS (
                SELECT 1 FROM "BankStatementMatches" m WHERE m."ReceiptId" = NEW."Id" AND m."BranchId" = NEW."BranchId") THEN
                RAISE EXCEPTION 'receipt confirmation requires an exact statement match' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankReceipts_Confirmation';
            END IF;
            RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER "TR_BankReceipts_Confirmation" AFTER UPDATE ON "BankReceipts"
            DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION bank_receipt_confirmation_guard();

        DO $$ DECLARE tab text; BEGIN
            FOREACH tab IN ARRAY ARRAY['CardProviders','CardSettlementBatches','BankStatementImports','BankStatementImportLines','BankStatementLines','BankMatchProposals','BankStatementMatches'] LOOP
                EXECUTE format('CREATE TRIGGER %I BEFORE UPDATE OR DELETE ON %I FOR EACH ROW EXECUTE FUNCTION financial_history_immutable()', 'TR_' || tab || '_Immutable', tab);
            END LOOP;
        END $$;
        """;

    public const string PreflightDown = """
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM "CardProviders") OR EXISTS (SELECT 1 FROM "CardSettlementBatches")
               OR EXISTS (SELECT 1 FROM "BankStatementImports") OR EXISTS (SELECT 1 FROM "BankStatementMatches") THEN
                RAISE EXCEPTION 'M97_BANKING_DOWNGRADE: retained banking evidence would be lost';
            END IF;
        END $$;
        DROP TRIGGER "TR_BankReceipts_Confirmation" ON "BankReceipts";
        DROP FUNCTION bank_receipt_confirmation_guard();
        DROP FUNCTION bank_match_coherent() CASCADE;
        """;
}
