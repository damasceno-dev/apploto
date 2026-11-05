namespace server.Infrastructure.Migrations;

internal static class CardFeeEvidenceSql
{
    public const string Install = """
        CREATE FUNCTION card_fee_evidence_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE batch record; receipt record; previous record;
        BEGIN
            SELECT * INTO batch FROM "CardSettlementBatches" WHERE "Id" = NEW."BatchId";
            SELECT * INTO receipt FROM "BankReceipts" WHERE "Id" = batch."BankReceiptId";
            SELECT * INTO previous FROM "CardSettlementFeeEvidence" WHERE "BatchId" = NEW."BatchId" ORDER BY "Sequence" DESC LIMIT 1;
            IF batch."BranchId" IS DISTINCT FROM NEW."BranchId" OR receipt."Status" IS DISTINCT FROM 1
                OR NEW."ConfirmedFee" >= batch."GrossAmount" OR NEW."Sequence" <> COALESCE(previous."Sequence", 0) + 1
                OR NEW."OccurredOn" < COALESCE(previous."OccurredOn", receipt."OccurredOn")
                OR NEW."PostedOn" < COALESCE(previous."PostedOn", receipt."PostedOn")
                OR EXISTS (SELECT 1 FROM "Settings" WHERE "BranchId" = NEW."BranchId" AND NEW."PostedOn" <= "LockDate") THEN
                RAISE EXCEPTION 'fee evidence must extend a pending batch in an open period' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_CardSettlementFeeEvidence_Chain';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_CardSettlementFeeEvidence_Coherent" BEFORE INSERT ON "CardSettlementFeeEvidence"
            FOR EACH ROW EXECUTE FUNCTION card_fee_evidence_guard();
        CREATE TRIGGER "TR_CardSettlementFeeEvidence_Immutable" BEFORE UPDATE OR DELETE ON "CardSettlementFeeEvidence"
            FOR EACH ROW EXECUTE FUNCTION financial_history_immutable();
        """;

    public static string MatchFunction => CardBankingSql.Install[
        CardBankingSql.Install.IndexOf("CREATE FUNCTION bank_match_coherent", StringComparison.Ordinal)..
        CardBankingSql.Install.IndexOf("CREATE CONSTRAINT TRIGGER \"TR_BankStatementMatches_Coherent\"", StringComparison.Ordinal)]
        .Replace("CREATE FUNCTION bank_match_coherent", "CREATE OR REPLACE FUNCTION bank_match_coherent", StringComparison.Ordinal)
        .Replace("expected numeric;", "expected numeric; effective_fee numeric; observed numeric;", StringComparison.Ordinal)
        .Replace("IF r.\"SourceKind\" IN (5, 6)", """
        IF r."SourceKind" = 2 THEN
            SELECT b."GrossAmount" - COALESCE(e."ConfirmedFee", b."ConfirmedFee"),
                COALESCE(e."ConfirmedFee", b."ConfirmedFee"), COALESCE(e."ObservedNetAmount", b."ObservedNetAmount")
                INTO expected, effective_fee, observed
            FROM "CardSettlementBatches" b LEFT JOIN LATERAL (
                SELECT * FROM "CardSettlementFeeEvidence" WHERE "BatchId" = b."Id" ORDER BY "Sequence" DESC LIMIT 1
            ) e ON true WHERE b."BankReceiptId" = r."Id";
        END IF;
        IF r."SourceKind" IN (5, 6)
        """, StringComparison.Ordinal)
        .Replace("OR EXISTS (SELECT 1 FROM \"CardSettlementBatches\" b WHERE b.\"BankReceiptId\" = r.\"Id\" AND (b.\"ConfirmedFee\" IS NULL OR (b.\"ObservedNetAmount\" IS NOT NULL AND b.\"ObservedNetAmount\" <> r.\"Amount\")))",
            "OR (r.\"SourceKind\" = 2 AND (effective_fee IS NULL OR (observed IS NOT NULL AND observed <> expected))) OR EXISTS (SELECT 1 FROM \"CardSettlementFeeEvidence\" e JOIN \"CardSettlementBatches\" b ON b.\"Id\" = e.\"BatchId\" WHERE b.\"BankReceiptId\" = r.\"Id\" AND NEW.\"PostedOn\" < e.\"PostedOn\")", StringComparison.Ordinal);

    public static string RestoreMatchFunction => CardBankingSql.Install[
        CardBankingSql.Install.IndexOf("CREATE FUNCTION bank_match_coherent", StringComparison.Ordinal)..
        CardBankingSql.Install.IndexOf("CREATE CONSTRAINT TRIGGER \"TR_BankStatementMatches_Coherent\"", StringComparison.Ordinal)]
        .Replace("CREATE FUNCTION bank_match_coherent", "CREATE OR REPLACE FUNCTION bank_match_coherent", StringComparison.Ordinal);

    public const string PreflightDown = """
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM "CardSettlementFeeEvidence") THEN
                RAISE EXCEPTION 'M97_FEE_EVIDENCE_DOWNGRADE: retained evidence would be lost';
            END IF;
        END $$;
        DROP FUNCTION card_fee_evidence_guard() CASCADE;
        """;
}
