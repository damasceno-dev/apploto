namespace server.Infrastructure.Migrations;

internal static class PersonalResponsibilitySql
{
    internal const string Up = """
        -- Preserve pre-upgrade forks as evidence, but serialize and refuse any new fork.
        CREATE FUNCTION opening_attribution_chain_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE head uuid; head_count integer; event_count integer;
        BEGIN
            PERFORM 1 FROM "SessionOpeningDiscrepancies" WHERE "Id" = NEW."DiscrepancyId" FOR UPDATE;
            SELECT count(*) INTO event_count FROM "OpeningDiscrepancyAttributionEvents" WHERE "DiscrepancyId" = NEW."DiscrepancyId";
            SELECT count(*), (array_agg(a."Id"))[1] INTO head_count, head
            FROM "OpeningDiscrepancyAttributionEvents" a WHERE a."DiscrepancyId" = NEW."DiscrepancyId"
                AND NOT EXISTS (SELECT 1 FROM "OpeningDiscrepancyAttributionEvents" n WHERE n."SupersedesEventId" = a."Id");
            IF (event_count > 0 AND head_count <> 1) THEN
                RAISE EXCEPTION 'ambiguous opening attribution history' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_OpeningDiscrepancyAttributionEvents_Ambiguous';
            END IF;
            IF NEW."SupersedesEventId" IS DISTINCT FROM head THEN
                RAISE EXCEPTION 'invalid opening attribution chain' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_OpeningDiscrepancyAttributionEvents_Chain';
            END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_OpeningDiscrepancyAttributionEvents_Chain" BEFORE INSERT ON "OpeningDiscrepancyAttributionEvents"
        FOR EACH ROW EXECUTE FUNCTION opening_attribution_chain_guard();

        CREATE TRIGGER "TR_OpeningDiscrepancyAdjustments_Immutable"
        BEFORE UPDATE OR DELETE ON "OpeningDiscrepancyAdjustments"
        FOR EACH ROW EXECUTE FUNCTION financial_history_immutable();

        CREATE FUNCTION personal_adjustment_chain_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE d "SessionOpeningDiscrepancies"%ROWTYPE; s "DailyCloses"%ROWTYPE;
                p "DailyCloses"%ROWTYPE; previous "OpeningDiscrepancyAdjustments"%ROWTYPE;
                attribution uuid; head_count integer; target_person uuid; original_person uuid; lock_date date;
        BEGIN
            SELECT * INTO STRICT d FROM "SessionOpeningDiscrepancies" WHERE "Id" = NEW."DiscrepancyId" FOR UPDATE;
            SELECT * INTO STRICT s FROM "DailyCloses" WHERE "Id" = d."SessionId";
            SELECT * INTO p FROM "DailyCloses" WHERE "Id" = d."PredecessorSessionId";
            SELECT "LockDate" INTO STRICT lock_date FROM "Settings" WHERE "BranchId" = d."BranchId";
            SELECT * INTO previous FROM "OpeningDiscrepancyAdjustments" WHERE "DiscrepancyId" = d."Id" ORDER BY "Sequence" DESC LIMIT 1;
            SELECT count(*), (array_agg(a."Id"))[1] INTO head_count, attribution FROM "OpeningDiscrepancyAttributionEvents" a
                WHERE a."DiscrepancyId" = d."Id" AND NOT EXISTS (
                    SELECT 1 FROM "OpeningDiscrepancyAttributionEvents" next WHERE next."SupersedesEventId" = a."Id");
            IF head_count > 1 THEN
                RAISE EXCEPTION 'ambiguous opening attribution history' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_OpeningDiscrepancyAttributionEvents_Ambiguous';
            END IF;
            target_person := CASE NEW."Target" WHEN 1 THEN p."CustodianOperatorId" WHEN 2 THEN s."CustodianOperatorId" ELSE NULL END;
            original_person := CASE WHEN d."AttributedRevision" = d."CurrentRevision" THEN
                CASE d."Attribution" WHEN 1 THEN p."CustodianOperatorId" WHEN 2 THEN s."CustodianOperatorId" ELSE NULL END ELSE NULL END;
            IF NEW."BranchId" <> d."BranchId" OR NEW."SessionId" <> s."Id"
               OR NEW."PredecessorSessionId" IS DISTINCT FROM d."PredecessorSessionId"
               OR NEW."HandoverId" IS DISTINCT FROM d."HandoverId"
               OR NEW."SourceDate" <> s."Date" OR NEW."ComparisonRevision" <> d."CurrentRevision"
               OR lock_date IS NULL OR NEW."PostedOn" <= lock_date OR NEW."PostedOn" < s."Date"
               OR (s."Date" > lock_date AND (p."Id" IS NULL OR p."Date" > lock_date))
               OR NEW."Sequence" <> COALESCE(previous."Sequence", 0) + 1
               OR NEW."PreviousAdjustmentId" IS DISTINCT FROM previous."Id"
               OR NEW."PriorAttributionId" IS DISTINCT FROM attribution
               OR NEW."OperatorId" IS DISTINCT FROM target_person
               OR (NEW."Target" IN (1,2) AND target_person IS NULL)
               OR NEW."SignedAmount" <> (CASE WHEN target_person IS NULL THEN 0 ELSE d."CurrentTotalDelta" END)
               OR (previous."Id" IS NOT NULL AND (NEW."PreviousOperatorId" IS DISTINCT FROM previous."OperatorId"
                   OR NEW."PreviousSignedAmount" <> previous."SignedAmount" OR NEW."PostedOn" < previous."PostedOn"))
               OR (previous."Id" IS NULL AND (NEW."PreviousOperatorId" IS DISTINCT FROM original_person
                   OR NEW."PreviousSignedAmount" <> (CASE WHEN original_person IS NULL THEN 0 ELSE d."CurrentTotalDelta" END)))
               OR NOT NEW."Active" OR length(btrim(NEW."Reason")) = 0
            THEN RAISE EXCEPTION 'invalid personal adjustment chain' USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_OpeningDiscrepancyAdjustments_Chain'; END IF;
            RETURN NEW;
        END $$;
        CREATE TRIGGER "TR_OpeningDiscrepancyAdjustments_Chain" BEFORE INSERT ON "OpeningDiscrepancyAdjustments"
        FOR EACH ROW EXECUTE FUNCTION personal_adjustment_chain_guard();
        """;

    internal const string Down = """
        DO $$ BEGIN
            IF EXISTS (SELECT 1 FROM "OpeningDiscrepancyAdjustments") THEN
                RAISE EXCEPTION 'M97_PERSONAL_DOWNGRADE: responsibility evidence must be retained';
            END IF;
        END $$;
        DROP TRIGGER "TR_OpeningDiscrepancyAdjustments_Chain" ON "OpeningDiscrepancyAdjustments";
        DROP TRIGGER "TR_OpeningDiscrepancyAdjustments_Immutable" ON "OpeningDiscrepancyAdjustments";
        DROP FUNCTION personal_adjustment_chain_guard();
        DROP TRIGGER "TR_OpeningDiscrepancyAttributionEvents_Chain" ON "OpeningDiscrepancyAttributionEvents";
        DROP FUNCTION opening_attribution_chain_guard();
        """;
}
