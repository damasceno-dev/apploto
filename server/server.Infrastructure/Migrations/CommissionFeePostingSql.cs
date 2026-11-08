namespace server.Infrastructure.Migrations;

internal static class CommissionFeePostingSql
{
    internal const string Up = """
        CREATE FUNCTION commission_fee_posting_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE s record; t record; locked date; expected numeric;
        BEGIN
            IF TG_OP<>'INSERT' THEN RAISE EXCEPTION 'commission posting is immutable' USING ERRCODE='23514', CONSTRAINT='CK_People_Immutable'; END IF;
            PERFORM pg_advisory_xact_lock_shared(('x' || substr(encode(sha256(convert_to('month-lock:' || replace(NEW."BranchId"::text,'-',''), 'UTF8')), 'hex'),1,16))::bit(64)::bigint);
            SELECT * INTO s FROM "DailyCloses" WHERE "Id"=NEW."SessionId";
            PERFORM pg_advisory_xact_lock(('x' || substr(encode(sha256(convert_to(replace(s."BranchId"::text,'-','') || ':' || replace(s."AccountId"::text,'-',''), 'UTF8')), 'hex'),1,16))::bit(64)::bigint);
            SELECT "LockDate" INTO locked FROM "Settings" WHERE "BranchId"=NEW."BranchId";
            SELECT * INTO t FROM "Transactions" WHERE "Id"=NEW."TransactionId";
            SELECT COALESCE(sum(p."SignedAmount"),0) INTO expected FROM "EmployeeCommissionFeePostings" p WHERE p."SessionId"=NEW."SessionId" AND p."TransactionId"=NEW."TransactionId" AND p."Component"=NEW."Component" AND p."SourceDate"=NEW."SourceDate";
            expected := (CASE WHEN t."Active" AND t."Status"=1 AND t."Date"=NEW."SourceDate" AND t."PostedFinancialClass"=2 AND
                ((t."PostedMonthlyFinancialRole"=1 AND NEW."Component"=1) OR (t."PostedMonthlyFinancialRole"=3 AND NEW."Component"=2)) THEN t."Value" * CASE WHEN t."Direction"=0 THEN 1 ELSE -1 END ELSE 0 END) - expected;
            IF s IS NULL OR t IS NULL OR locked IS NULL OR NEW."PostedOn"<=locked OR NEW."SourceDate">NEW."PostedOn" OR NOT NEW."Active"
                OR s."BranchId"<>NEW."BranchId" OR s."CustodianOperatorId" IS DISTINCT FROM NEW."OperatorId" OR NOT s."CustodyHistoryComplete"
                OR s."Status"=0 OR s."SubmittedAttemptId" IS DISTINCT FROM NEW."AttemptId"
                OR t."BranchId"<>NEW."BranchId" OR t."SessionId" IS DISTINCT FROM NEW."SessionId"
                OR NEW."Component" NOT IN (1,2) OR NEW."SignedAmount"=0 OR NEW."SignedAmount"<>expected
                OR (t."ClassificationProvenance" IS DISTINCT FROM NEW."ClassificationProvenance" AND NOT EXISTS (
                    SELECT 1 FROM "EmployeeCommissionFeePostings" p WHERE p."SessionId"=NEW."SessionId" AND p."TransactionId"=NEW."TransactionId"
                        AND p."Component"=NEW."Component" AND p."SourceDate"=NEW."SourceDate" AND p."ClassificationProvenance" IS NOT DISTINCT FROM NEW."ClassificationProvenance"))
                OR NOT EXISTS (SELECT 1 FROM "SessionCountAttempts" a WHERE a."Id"=NEW."AttemptId" AND a."SessionId"=s."Id" AND a."BranchId"=s."BranchId") THEN
                RAISE EXCEPTION 'invalid commission posting evidence' USING ERRCODE='23514', CONSTRAINT='CK_People_Source'; END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_EmployeeCommissionFeePostings_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "EmployeeCommissionFeePostings" FOR EACH ROW EXECUTE FUNCTION commission_fee_posting_guard();
        """;
}
