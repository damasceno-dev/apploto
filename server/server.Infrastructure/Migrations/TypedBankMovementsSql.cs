namespace server.Infrastructure.Migrations;

internal static class TypedBankMovementsSql
{
    public static string Install => CardBankingSql.Install[..CardBankingSql.Install.IndexOf("CREATE FUNCTION bank_match_coherent", StringComparison.Ordinal)]
        .Replace("ELSE\n", "ELSIF NEW.\"SourceKind\" IN (3, 4, 5) THEN\n                SELECT t.\"Value\" INTO expected FROM \"BankMovements\" m INNER JOIN \"Transactions\" t ON t.\"Id\" = m.\"BankTransactionId\" WHERE m.\"BankReceiptId\" = NEW.\"Id\" AND m.\"BranchId\" = NEW.\"BranchId\" AND t.\"AccountId\" = NEW.\"BankAccountId\";\n            ELSE\n", StringComparison.Ordinal) + """
        CREATE FUNCTION bank_movement_coherent() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE bankrow record; source record; receipt record;
        BEGIN
            SELECT * INTO bankrow FROM "Transactions" WHERE "Id" = NEW."BankTransactionId";
            SELECT * INTO receipt FROM "BankReceipts" WHERE "Id" = NEW."BankReceiptId";
            IF bankrow."BranchId" IS DISTINCT FROM NEW."BranchId" OR receipt."BranchId" IS DISTINCT FROM NEW."BranchId"
               OR bankrow."GenerationKind" IS DISTINCT FROM 8::smallint OR bankrow."Status" <> 1 OR bankrow."SessionId" IS NOT NULL
               OR bankrow."AccountId" IS DISTINCT FROM receipt."BankAccountId" OR bankrow."Value" IS DISTINCT FROM receipt."Amount"
               OR bankrow."Date" IS DISTINCT FROM receipt."PostedOn" OR receipt."SourceKind" <> NEW."Kind" + 2
               OR bankrow."PostedFinancialClass" IS DISTINCT FROM (CASE NEW."Kind" WHEN 1 THEN 4 WHEN 2 THEN 1 WHEN 3 THEN 3 END)::smallint
               OR bankrow."Direction" IS DISTINCT FROM (CASE NEW."Kind" WHEN 3 THEN 1 ELSE 0 END)::smallint THEN
                RAISE EXCEPTION 'typed bank movement disagrees with its frozen posting' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankMovements_Coherent';
            END IF;
            IF NEW."Kind" = 1 THEN
                SELECT t.*, a."Type" AS account_type INTO source FROM "Transactions" t INNER JOIN "Accounts" a ON a."Id" = t."AccountId" WHERE t."Id" = NEW."SourceTransactionId";
                IF NOT FOUND OR source."BranchId" <> NEW."BranchId" OR source.account_type <> 3 OR source."GenerationKind" <> 8
                   OR source."Direction" <> 1 OR source."Value" <> bankrow."Value" OR source."PostedFinancialClass" <> 4 OR source."Status" <> 1
                   OR source."Date" <> bankrow."Date" OR source."SessionId" IS NOT NULL THEN
                    RAISE EXCEPTION 'collection requires its protected cofre source' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankMovements_Coherent';
                END IF;
            ELSIF NEW."SourceTransactionId" IS NOT NULL THEN
                RAISE EXCEPTION 'bank fees and reimbursements have no fabricated source' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankMovements_Coherent';
            END IF;
            RETURN NULL;
        END $$;
        CREATE CONSTRAINT TRIGGER "TR_BankMovements_Coherent" AFTER INSERT ON "BankMovements" DEFERRABLE INITIALLY DEFERRED
            FOR EACH ROW EXECUTE FUNCTION bank_movement_coherent();
        CREATE FUNCTION bank_movement_transaction_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF OLD."GenerationKind" = 8 THEN
                RAISE EXCEPTION 'typed bank movement rows are immutable' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankMovements_PostingImmutable';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_Transactions_BankMovementImmutable" BEFORE UPDATE OR DELETE ON "Transactions"
            FOR EACH ROW EXECUTE FUNCTION bank_movement_transaction_immutable();
        CREATE TRIGGER "TR_BankMovements_Immutable" BEFORE UPDATE OR DELETE ON "BankMovements"
            FOR EACH ROW EXECUTE FUNCTION financial_history_immutable();
        """;
}
