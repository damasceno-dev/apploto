namespace server.Infrastructure.Migrations;

internal static class PeopleRecordsSql
{
    internal const string Up = """
        CREATE FUNCTION people_record_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE j jsonb; p jsonb; b uuid; person uuid; locked date; k bigint; expected numeric; source_amount numeric; category_branch uuid; fee record;
        BEGIN
            IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'people history is immutable' USING ERRCODE='23514', CONSTRAINT='CK_People_Immutable'; END IF;
            j := to_jsonb(NEW); b := NEW."BranchId";
            k := ('x' || substr(encode(sha256(convert_to('month-lock:' || replace(b::text, '-', ''), 'UTF8')), 'hex'), 1, 16))::bit(64)::bigint;
            PERFORM pg_advisory_xact_lock_shared(k);
            PERFORM pg_advisory_xact_lock(hashtextextended('people:' || b::text, 0));
            SELECT "LockDate" INTO locked FROM "Settings" WHERE "BranchId" = b;
            IF locked IS NULL OR NEW."PostedOn" <= locked OR NOT NEW."Active" OR length(btrim(NEW."Reason"))=0 OR length(btrim(NEW."Evidence"))=0
               OR NOT EXISTS (SELECT 1 FROM "BranchUsers" m JOIN "Users" u ON u."Id"=m."UserId" JOIN "Branches" br ON br."Id"=m."BranchId"
                  WHERE m."BranchId"=b AND m."UserId"=NEW."ActorUserId" AND m."Active" AND u."Active" AND br."Active" AND m."Role" IN (0,1))
            THEN RAISE EXCEPTION 'invalid people authority or posting period' USING ERRCODE='23514', CONSTRAINT='CK_People_PeriodAuthority'; END IF;
            person := (j->>'OperatorId')::uuid;
            IF person IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Operators" WHERE "Id"=person AND "BranchId"=b) THEN
                RAISE EXCEPTION 'cross branch employee' USING ERRCODE='23514', CONSTRAINT='CK_People_Source'; END IF;
            IF (j->>'PreviousId') IS NOT NULL THEN
                EXECUTE format('SELECT to_jsonb(r) FROM %I r WHERE "Id"=$1', TG_TABLE_NAME) INTO p USING (j->>'PreviousId')::uuid;
                IF p IS NULL OR p->>'BranchId' <> b::text OR p->>'OperatorId' IS DISTINCT FROM j->>'OperatorId' OR NEW."PostedOn" < (p->>'PostedOn')::date
                   OR COALESCE((p->>'Cancelled')::boolean,false)
                THEN RAISE EXCEPTION 'invalid people revision' USING ERRCODE='23514', CONSTRAINT='CK_People_Chain'; END IF;
            END IF;
            IF TG_TABLE_NAME='EmployeePayCategories' THEN
                IF NEW."Kind" NOT IN (1,2) OR length(btrim(NEW."Name"))=0 OR (p IS NOT NULL AND p->>'Kind'<>j->>'Kind') THEN
                    RAISE EXCEPTION 'invalid pay category' USING ERRCODE='23514', CONSTRAINT='CK_People_Source'; END IF;
            ELSIF TG_TABLE_NAME='EmployeePayItems' THEN
                SELECT "BranchId" INTO category_branch FROM "EmployeePayCategories" WHERE "Id"=NEW."CategoryId" AND "Kind"=NEW."Kind";
                IF category_branch IS DISTINCT FROM b OR NOT EXISTS (SELECT 1 FROM "EmployeePayCategories" c WHERE c."Id"=NEW."CategoryId" AND c."Name"=NEW."CategoryName") OR NEW."Amount"<=0 OR NEW."Kind" NOT IN (1,2) OR extract(day from NEW."SourceMonth")<>1
                   OR date_trunc('month', NEW."SourceDate")::date <> NEW."SourceMonth" OR NEW."SourceDate">NEW."PostedOn"
                   OR (NEW."Kind"=2 AND COALESCE(length(btrim(NEW."DeductionPolicy")),0)=0)
                   OR (p IS NULL AND (NEW."SourceMonth"<=locked OR NEW."Cancelled"))
                   OR (p IS NOT NULL AND (p->>'SourceMonth'<>j->>'SourceMonth' OR p->>'SourceDate'<>j->>'SourceDate' OR p->>'CategoryId'<>j->>'CategoryId' OR p->>'Kind'<>j->>'Kind' OR (p->>'CommissionCalculationId' IS NULL) <> (j->>'CommissionCalculationId' IS NULL)))
                THEN RAISE EXCEPTION 'invalid pay evidence' USING ERRCODE='23514', CONSTRAINT='CK_People_Source'; END IF;
                expected := (CASE WHEN NEW."Cancelled" THEN 0 ELSE NEW."Amount" * CASE NEW."Kind" WHEN 1 THEN 1 ELSE -1 END END) -
                   COALESCE((p->>'Amount')::numeric * CASE (p->>'Kind')::int WHEN 1 THEN 1 ELSE -1 END,0);
                IF NEW."SignedContribution"<>expected THEN RAISE EXCEPTION 'invalid pay contribution' USING ERRCODE='23514', CONSTRAINT='CK_People_Chain'; END IF;
                IF (NEW."CommissionCalculationId" IS NOT NULL AND (NEW."Kind"<>1 OR NEW."CommissionEvidenceJson" IS NULL))
                    OR (p IS NOT NULL AND NEW."CommissionCalculationId" IS NULL AND p->>'CommissionEvidenceJson' IS DISTINCT FROM j->>'CommissionEvidenceJson') THEN
                    RAISE EXCEPTION 'invalid commission evidence' USING ERRCODE='23514', CONSTRAINT='CK_People_Source'; END IF;
            ELSIF TG_TABLE_NAME='EmployeeCommissionRules' THEN
                IF NEW."EffectiveTo"<NEW."EffectiveFrom" OR NEW."RatePercent"<0 OR NEW."RatePercent">100
                   OR NOT (NEW."IncludeBolaoFee" OR NEW."IncludeSpecialFee" OR NEW."IncludeExplicitSources")
                   OR (p IS NULL AND NEW."EffectiveFrom"<=locked)
                   OR (p IS NOT NULL AND (p->>'EffectiveFrom'<>j->>'EffectiveFrom' OR NEW."EffectiveTo">(p->>'EffectiveTo')::date))
                   OR (p IS NOT NULL AND NEW."EffectiveFrom"<=locked AND
                       (NEW."EffectiveTo">=(p->>'EffectiveTo')::date OR NEW."EffectiveTo"<locked
                        OR p->>'RatePercent'<>j->>'RatePercent' OR p->>'IncludeBolaoFee'<>j->>'IncludeBolaoFee'
                        OR p->>'IncludeSpecialFee'<>j->>'IncludeSpecialFee' OR p->>'IncludeExplicitSources'<>j->>'IncludeExplicitSources' OR p->>'Enabled'<>j->>'Enabled'))
                THEN RAISE EXCEPTION 'invalid rule period' USING ERRCODE='23514', CONSTRAINT='CK_People_Source'; END IF;
                IF NEW."Enabled" AND EXISTS (SELECT 1 FROM "EmployeeCommissionRules" r WHERE r."BranchId"=b AND r."OperatorId"=person AND r."Enabled"
                    AND r."Id" IS DISTINCT FROM NEW."PreviousId" AND r."EffectiveFrom"<=NEW."EffectiveTo" AND r."EffectiveTo">=NEW."EffectiveFrom"
                    AND NOT EXISTS (SELECT 1 FROM "EmployeeCommissionRules" n WHERE n."PreviousId"=r."Id")) THEN
                    RAISE EXCEPTION 'overlapping commission rule' USING ERRCODE='23514', CONSTRAINT='CK_People_RuleOverlap'; END IF;
            ELSIF TG_TABLE_NAME='EmployeeCommissionSources' THEN
                IF NEW."SourceDate">NEW."PostedOn" OR length(btrim(NEW."ExternalIdentity"))=0 OR NEW."SourceRole" NOT IN ('BolaoLeftover','EnteredPrize')
                   OR (p IS NOT NULL AND (p->>'SourceDate'<>j->>'SourceDate' OR p->>'SourceRole'<>j->>'SourceRole' OR p->>'ExternalIdentity'<>j->>'ExternalIdentity'))
                   OR NEW."SignedContribution"<>NEW."SignedAmount"-COALESCE((p->>'SignedAmount')::numeric,0)
                THEN RAISE EXCEPTION 'invalid explicit commission source' USING ERRCODE='23514', CONSTRAINT='CK_People_Chain'; END IF;
            ELSIF TG_TABLE_NAME='HourBankEntries' THEN
                IF NEW."Kind" NOT IN (1,2,3) OR NEW."SourceDate">NEW."PostedOn" OR (NEW."Kind"=2 AND NEW."SignedHours">=0) OR (NEW."Kind"<>1 AND NEW."SignedHours"=0)
                   OR (NEW."Kind"=1 AND (NEW."Cancelled" OR (p IS NULL AND (NEW."SourceDate"<=locked OR length(btrim(NEW."ExternalIdentity"))=0))))
                   OR (p IS NOT NULL AND (p->>'Kind'<>j->>'Kind' OR p->>'SourceDate'<>j->>'SourceDate' OR p->>'ExternalIdentity'<>j->>'ExternalIdentity'))
                   OR (p IS NULL AND NEW."Cancelled")
                   OR NEW."SignedContribution"<>(CASE WHEN NEW."Cancelled" THEN 0 ELSE NEW."SignedHours" END)-COALESCE((p->>'SignedHours')::numeric,0)
                THEN RAISE EXCEPTION 'invalid hour entry' USING ERRCODE='23514', CONSTRAINT='CK_People_Chain'; END IF;
                IF NEW."Kind"<>1 AND NOT EXISTS (SELECT 1 FROM "HourBankEntries" h WHERE h."BranchId"=b AND h."OperatorId"=person AND h."Kind"=1 AND h."PreviousId" IS NULL AND h."SourceDate"<=NEW."SourceDate" AND h."PostedOn"<=NEW."PostedOn") THEN
                    RAISE EXCEPTION 'missing hour opening' USING ERRCODE='23514', CONSTRAINT='CK_People_Source'; END IF;
            ELSIF TG_TABLE_NAME='SpecialDraws' THEN
                IF NEW."PeriodTo"<NEW."PeriodFrom" OR NEW."Prize"<0 OR length(btrim(NEW."Name"))=0 OR length(btrim(NEW."Modality"))=0 OR length(btrim(NEW."Contest"))=0 THEN
                    RAISE EXCEPTION 'invalid draw' USING ERRCODE='23514', CONSTRAINT='CK_People_Source'; END IF;
                IF p IS NOT NULL AND EXISTS (
                    WITH RECURSIVE chain AS (
                        SELECT d."Id", d."PreviousId" FROM "SpecialDraws" d WHERE d."Id"=NEW."PreviousId"
                        UNION ALL SELECT d."Id", d."PreviousId" FROM "SpecialDraws" d JOIN chain c ON d."Id"=c."PreviousId")
                    SELECT 1 FROM "SpecialDrawAllocations" a JOIN chain c ON a."DrawId"=c."Id" WHERE a."SourceDate"<=locked)
                    AND (NEW."Enabled" OR p->>'Name'<>j->>'Name' OR p->>'Modality'<>j->>'Modality' OR p->>'Contest'<>j->>'Contest'
                        OR p->>'PeriodFrom'<>j->>'PeriodFrom' OR p->>'PeriodTo'<>j->>'PeriodTo' OR p->>'Prize'<>j->>'Prize') THEN
                    RAISE EXCEPTION 'locked draw identity' USING ERRCODE='23514', CONSTRAINT='CK_People_Source'; END IF;
            ELSIF TG_TABLE_NAME='SpecialDrawAllocations' THEN
                PERFORM pg_advisory_xact_lock(('x' || substr(encode(sha256(convert_to(replace(b::text,'-','') || ':' ||
                    (SELECT replace(s."AccountId"::text,'-','') FROM "SessionFieldEffects" e JOIN "DailyCloses" s ON s."Id"=e."SessionId" WHERE e."Id"=NEW."SessionFieldEffectId" AND e."BranchId"=b), 'UTF8')), 'hex'),1,16))::bit(64)::bigint);
                SELECT e."Value", e."Revision", e."SessionId", e."CurrentTransactionId", s."Date", s."Status", s."BranchId" INTO fee
                FROM "SessionFieldEffects" e JOIN "DailyCloses" s ON s."Id"=e."SessionId" WHERE e."Id"=NEW."SessionFieldEffectId" AND e."Field"=3;
                IF fee IS NULL OR fee."BranchId"<>b OR fee."SessionId"<>NEW."SessionId" OR fee."Date"<>NEW."SourceDate" OR (NOT NEW."Cancelled" AND fee."Revision"<>NEW."SourceRevision")
                   OR (NOT NEW."Cancelled" AND (fee."Value"<>NEW."SourceValue" OR fee."Status"=0 OR NOT EXISTS (
                       SELECT 1 FROM "Transactions" t WHERE t."Id"=fee."CurrentTransactionId" AND t."BranchId"=b AND t."SessionId"=NEW."SessionId"
                           AND t."Active" AND t."Status"=1 AND t."PostedMonthlyFinancialRole"=3 AND t."PostedFinancialClass"=2))) OR NEW."Amount"<=0 OR NEW."SourceDate">NEW."PostedOn"
                   OR NOT EXISTS (SELECT 1 FROM "SpecialDraws" d WHERE d."Id"=NEW."DrawId" AND d."BranchId"=b AND NEW."PostedOn">=d."PostedOn" AND NEW."SourceDate" BETWEEN d."PeriodFrom" AND d."PeriodTo")
                   OR (p IS NULL AND (NEW."SourceDate"<=locked OR NEW."Cancelled"))
                   OR (p IS NOT NULL AND (p->>'DrawId'<>j->>'DrawId' OR p->>'SessionFieldEffectId'<>j->>'SessionFieldEffectId' OR p->>'SourceRevision'<>j->>'SourceRevision'))
                   OR NEW."SignedContribution"<>(CASE WHEN NEW."Cancelled" THEN 0 ELSE NEW."Amount" END)-COALESCE((p->>'Amount')::numeric,0)
                THEN RAISE EXCEPTION 'invalid draw source' USING ERRCODE='23514', CONSTRAINT='CK_People_Source'; END IF;
                SELECT COALESCE(sum(a."Amount"),0) INTO source_amount FROM "SpecialDrawAllocations" a WHERE a."SessionFieldEffectId"=NEW."SessionFieldEffectId"
                   AND a."Id" IS DISTINCT FROM NEW."PreviousId" AND NOT a."Cancelled" AND NOT EXISTS (SELECT 1 FROM "SpecialDrawAllocations" n WHERE n."PreviousId"=a."Id");
                IF NOT NEW."Cancelled" AND source_amount+NEW."Amount">fee."Value" THEN
                    RAISE EXCEPTION 'draw allocation exceeds fee' USING ERRCODE='23514', CONSTRAINT='CK_People_Allocation'; END IF;
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_EmployeePayCategories_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "EmployeePayCategories" FOR EACH ROW EXECUTE FUNCTION people_record_guard();
        CREATE TRIGGER "TR_EmployeePayItems_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "EmployeePayItems" FOR EACH ROW EXECUTE FUNCTION people_record_guard();
        CREATE TRIGGER "TR_EmployeeCommissionRules_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "EmployeeCommissionRules" FOR EACH ROW EXECUTE FUNCTION people_record_guard();
        CREATE TRIGGER "TR_EmployeeCommissionSources_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "EmployeeCommissionSources" FOR EACH ROW EXECUTE FUNCTION people_record_guard();
        CREATE TRIGGER "TR_HourBankEntries_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "HourBankEntries" FOR EACH ROW EXECUTE FUNCTION people_record_guard();
        CREATE TRIGGER "TR_SpecialDraws_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "SpecialDraws" FOR EACH ROW EXECUTE FUNCTION people_record_guard();
        CREATE TRIGGER "TR_SpecialDrawAllocations_Guard" BEFORE INSERT OR UPDATE OR DELETE ON "SpecialDrawAllocations" FOR EACH ROW EXECUTE FUNCTION people_record_guard();
        """;
}
