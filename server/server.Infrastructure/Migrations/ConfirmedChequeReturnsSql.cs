namespace server.Infrastructure.Migrations;

internal static class ConfirmedChequeReturnsSql
{
    public static string Install => TypedBankMovementsSql.Install[..TypedBankMovementsSql.Install.IndexOf("CREATE FUNCTION bank_movement_coherent", StringComparison.Ordinal)]
        .Replace("ELSIF NEW.\"SourceKind\" IN (3, 4, 5) THEN", """
        ELSIF NEW."SourceKind" = 6 THEN
            SELECT e."Amount" INTO expected FROM "SettlementEvents" e
            INNER JOIN "SettlementEvents" d ON d."Id" = e."ReversesEventId"
            INNER JOIN "BankReceipts" original ON original."Id" = d."BankReceiptId"
            WHERE e."Id" = NEW."SourceSettlementEventId" AND e."Kind" = 4 AND e."BranchId" = NEW."BranchId"
              AND original."Status" = 2 AND original."BankAccountId" = NEW."BankAccountId";
        ELSIF NEW."SourceKind" IN (3, 4, 5) THEN
        """, StringComparison.Ordinal) + """
        CREATE FUNCTION bank_receipt_source_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            IF NEW."SourceSettlementEventId" IS DISTINCT FROM OLD."SourceSettlementEventId" OR NEW."Active" IS DISTINCT FROM OLD."Active" THEN
                RAISE EXCEPTION 'bank receipt source is immutable' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_BankReceipts_Immutable';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_BankReceipts_SourceImmutable" BEFORE UPDATE ON "BankReceipts" FOR EACH ROW EXECUTE FUNCTION bank_receipt_source_immutable();
        """;
}
