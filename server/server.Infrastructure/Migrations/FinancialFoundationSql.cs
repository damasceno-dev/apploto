namespace server.Infrastructure.Migrations;

/// <summary>
/// M9.6 Phases 6, 7 and 9 database guards and the reviewed seed function. Kept apart from the migration so the
/// persistence tests can run the seed function against prior-schema rows more than once.
/// </summary>
internal static class FinancialFoundationSql
{
    public const string SeedMapSetting = "loto.m96_financial_seed_map";

    public const string InstallGuards =
        """
        CREATE FUNCTION session_field_effect_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE effect record;
        BEGIN
            IF TG_OP = 'UPDATE' AND (NEW."SessionId" <> OLD."SessionId" OR NEW."Field" <> OLD."Field"
                OR NEW."BranchId" <> OLD."BranchId") THEN
                RAISE EXCEPTION 'a field slot never moves to another session or field'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SessionFieldEffects_SlotImmutable';
            END IF;
            IF NEW."CurrentTransactionId" IS NOT NULL THEN
                SELECT t."SessionId", t."GenerationKind", t."Status", t."BranchId" INTO effect
                FROM "Transactions" t WHERE t."Id" = NEW."CurrentTransactionId";
                IF effect."SessionId" IS DISTINCT FROM NEW."SessionId"
                    OR effect."BranchId" <> NEW."BranchId"
                    OR effect."GenerationKind" IS DISTINCT FROM (NEW."Field" + 2)::smallint
                    OR effect."Status" <> 1 THEN
                    RAISE EXCEPTION 'a field slot points only at its own live generated effect'
                        USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_SessionFieldEffects_EffectCoherent';
                END IF;
            END IF;
            RETURN NEW;
        END $$;

        CREATE TRIGGER "TR_SessionFieldEffects_Guard"
            BEFORE INSERT OR UPDATE ON "SessionFieldEffects"
            FOR EACH ROW EXECUTE FUNCTION session_field_effect_guard();

        CREATE FUNCTION financial_history_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
        BEGIN
            RAISE EXCEPTION '% rows are immutable history', TG_TABLE_NAME
                USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_' || TG_TABLE_NAME || '_Immutable';
        END $$;

        CREATE TRIGGER "TR_SessionFieldEffectRevisions_Immutable"
            BEFORE UPDATE OR DELETE ON "SessionFieldEffectRevisions"
            FOR EACH ROW EXECUTE FUNCTION financial_history_immutable();

        CREATE TRIGGER "TR_CustodyTransfers_Immutable"
            BEFORE UPDATE OR DELETE ON "CustodyTransfers"
            FOR EACH ROW EXECUTE FUNCTION financial_history_immutable();

        CREATE FUNCTION custody_transfer_guard() RETURNS trigger LANGUAGE plpgsql AS $$
        DECLARE source record; destination record; source_type integer; destination_type integer;
        BEGIN
            SELECT t."AccountId", t."BranchId", t."Value", t."Direction", t."SessionId", t."GenerationKind"
                INTO source FROM "Transactions" t WHERE t."Id" = NEW."SourceTransactionId";
            SELECT t."AccountId", t."BranchId", t."Value", t."Direction", t."GenerationKind", t."GeneratedFromTransactionId"
                INTO destination FROM "Transactions" t WHERE t."Id" = NEW."DestinationTransactionId";
            SELECT "Type" INTO source_type FROM "Accounts" WHERE "Id" = NEW."SourceAccountId" AND "BranchId" = NEW."BranchId";
            SELECT "Type" INTO destination_type FROM "Accounts" WHERE "Id" = NEW."DestinationAccountId" AND "BranchId" = NEW."BranchId";
            IF source_type IS DISTINCT FROM 0 OR destination_type IS DISTINCT FROM 3
                OR source."AccountId" <> NEW."SourceAccountId" OR destination."AccountId" <> NEW."DestinationAccountId"
                OR source."BranchId" <> NEW."BranchId" OR destination."BranchId" <> NEW."BranchId"
                OR source."Value" <> NEW."Value" OR destination."Value" <> NEW."Value"
                OR source."Direction" <> 1 OR destination."Direction" <> 0
                OR source."GenerationKind" IS NOT NULL
                OR destination."GenerationKind" IS DISTINCT FROM 6::smallint
                OR destination."GeneratedFromTransactionId" IS DISTINCT FROM NEW."SourceTransactionId"
                OR source."SessionId" IS DISTINCT FROM NEW."SourceSessionId" THEN
                RAISE EXCEPTION 'a cofre transfer links a cashbox Out and its generated cofre In of equal value'
                    USING ERRCODE = 'check_violation', CONSTRAINT = 'CK_CustodyTransfers_LegsCoherent';
            END IF;
            RETURN NEW;
        END $$;

        CREATE TRIGGER "TR_CustodyTransfers_LegsCoherent"
            BEFORE INSERT ON "CustodyTransfers"
            FOR EACH ROW EXECUTE FUNCTION custody_transfer_guard();
        """;

    public const string UninstallGuards =
        """
        DROP TRIGGER IF EXISTS "TR_CustodyTransfers_LegsCoherent" ON "CustodyTransfers";
        DROP FUNCTION IF EXISTS custody_transfer_guard();
        DROP TRIGGER IF EXISTS "TR_CustodyTransfers_Immutable" ON "CustodyTransfers";
        DROP TRIGGER IF EXISTS "TR_SessionFieldEffectRevisions_Immutable" ON "SessionFieldEffectRevisions";
        DROP FUNCTION IF EXISTS financial_history_immutable();
        DROP TRIGGER IF EXISTS "TR_SessionFieldEffects_Guard" ON "SessionFieldEffects";
        DROP FUNCTION IF EXISTS session_field_effect_guard();
        DROP FUNCTION IF EXISTS loto_m96_apply_financial_seeds(jsonb);
        """;

    /// <summary>
    /// <c>loto_m96_apply_financial_seeds(map)</c>: gives existing branches the M9.6 financial seeds from an
    /// operator-reviewed identity map, exactly once and deterministically. Old seed rows have random ids and mutable
    /// names, so the map names the existing category of each kind and the existing Tarifa Bolão, Operador and
    /// Depósito Dinheiro types to convert (null = create new). A same-name custom type is never adopted: a system type
    /// that cannot be placed raises; an ordinary seed (method, expense) is skipped and reported. Re-running with the
    /// same map changes nothing. Single ordinary fee/report rows of an open Ready session become that field's effect;
    /// several rows or a Draft row stay as they are with an <c>AmbiguousSessionFieldRows</c> issue; locked sessions are
    /// left untouched. Returns one row per decision.
    /// </summary>
    public const string InstallSeedFunction =
        """
        CREATE FUNCTION loto_m96_apply_financial_seeds(mapping jsonb)
        RETURNS TABLE(branch_id uuid, item text, outcome text) LANGUAGE plpgsql AS $fn$
        DECLARE
            entry jsonb;
            b uuid;
            cats jsonb;
            cat record;
            cat_id uuid;
            spec record;
            type_id uuid;
            mapped uuid;
            existing record;
            lock_date date;
            session_row record;
            field_rows uuid[];
            draft_count integer;
            adopted record;
            slot_id uuid;
            safe_cat uuid;
            safe_cat_name text;
        BEGIN
            LOCK TABLE "Branches", "Categories", "TransactionTypes", "Transactions", "DailyCloses",
                "SessionFieldEffects", "SessionFieldEffectRevisions", "CustodyMappingIssues", "Settings"
                IN SHARE ROW EXCLUSIVE MODE;
            IF jsonb_typeof(mapping) <> 'array' THEN
                RAISE EXCEPTION 'M96_PREFLIGHT: % must be a reviewed JSON array', 'loto.m96_financial_seed_map';
            END IF;
            IF EXISTS (SELECT 1 FROM jsonb_array_elements(mapping) e GROUP BY e->>'branchId' HAVING count(*) <> 1) THEN
                RAISE EXCEPTION 'M96_PREFLIGHT: one reviewed entry per branch';
            END IF;

            FOR entry IN SELECT value FROM jsonb_array_elements(mapping) LOOP
                b := (entry->>'branchId')::uuid;
                IF NOT EXISTS (SELECT 1 FROM "Branches" WHERE "Id" = b) THEN
                    RAISE EXCEPTION 'M96_PREFLIGHT: unknown branch %', b;
                END IF;
                IF NOT (entry ? 'receitaCategoryId' AND entry ? 'entradasCategoryId' AND entry ? 'saidasCategoryId'
                    AND entry ? 'despesasAdministrativasCategoryId' AND entry ? 'despesasPessoalCategoryId'
                    AND entry ? 'despesasFinanceirasCategoryId' AND entry ? 'bolaoFeeTypeId'
                    AND entry ? 'terminalReportTypeId' AND entry ? 'safeDepositTypeId') THEN
                    RAISE EXCEPTION 'M96_PREFLIGHT: every category/type identity (or a reviewed null) is required for branch %', b;
                END IF;

                -- Categories: an explicit id, else the category already holding this kind's seed keys, else a new
                -- one with the default name (an active same-name category is never adopted silently).
                cats := '{}'::jsonb;
                FOR cat IN SELECT * FROM (VALUES
                    ('receita', 'Receita', 0, 'receitaCategoryId'),
                    ('entradas', 'Entradas', 0, 'entradasCategoryId'),
                    ('saidas', 'Saídas', 1, 'saidasCategoryId'),
                    ('administrativas', 'Despesas Administrativas', 1, 'despesasAdministrativasCategoryId'),
                    ('pessoal', 'Despesas Pessoal', 1, 'despesasPessoalCategoryId'),
                    ('financeiras', 'Despesas Financeiras', 1, 'despesasFinanceirasCategoryId'))
                    AS c(kind, name, direction, map_key) LOOP
                    cat_id := (entry->>cat.map_key)::uuid;
                    IF cat_id IS NULL THEN
                        SELECT t."CategoryId" INTO cat_id FROM "TransactionTypes" t
                            JOIN "Categories" c ON c."Id" = t."CategoryId"
                            WHERE c."BranchId" = b AND t."SeedKey" LIKE cat.kind || '.%' LIMIT 1;
                    END IF;
                    IF cat_id IS NULL THEN
                        IF EXISTS (SELECT 1 FROM "Categories" WHERE "BranchId" = b AND "Name" = cat.name AND "Active") THEN
                            RAISE EXCEPTION 'M96_PREFLIGHT: category name collision (%); review its id for branch %', cat.name, b;
                        END IF;
                        cat_id := gen_random_uuid();
                        INSERT INTO "Categories" ("Id", "Name", "DefaultDirection", "BranchId", "Active", "CreatedAt")
                            VALUES (cat_id, cat.name, cat.direction, b, true, now());
                        branch_id := b; item := 'category:' || cat.kind; outcome := 'created'; RETURN NEXT;
                    ELSIF NOT EXISTS (SELECT 1 FROM "Categories" c WHERE c."Id" = cat_id AND c."BranchId" = b
                            AND c."Active" AND c."DefaultDirection" = cat.direction)
                        OR EXISTS (SELECT 1 FROM "TransactionTypes" t WHERE t."CategoryId" = cat_id AND t."SystemRole" IN (1, 2, 8)) THEN
                        RAISE EXCEPTION 'M96_PREFLIGHT: % must be an active, ordinary % category of branch %', cat.map_key,
                            CASE cat.direction WHEN 0 THEN 'In' ELSE 'Out' END, b;
                    END IF;
                    cats := cats || jsonb_build_object(cat.kind, cat_id);
                END LOOP;

                -- System types converted from a reviewed existing type, or created when the map says null.
                FOR spec IN SELECT * FROM (VALUES
                    (5::smallint, 'receita', 'Tarifa Bolão', 'receita.tarifa-bolao', 'bolaoFeeTypeId', 1::smallint, 0::smallint),
                    (3::smallint, 'entradas', 'Relatório do terminal', 'entradas.relatorio-terminal', 'terminalReportTypeId', 0::smallint, 0::smallint),
                    (7::smallint, 'saidas', 'Depósito Dinheiro', 'saidas.deposito-dinheiro', 'safeDepositTypeId', 0::smallint, 3::smallint),
                    (6::smallint, 'receita', 'Tarifa Especial', 'receita.tarifa-especial', NULL, 3::smallint, 0::smallint),
                    (4::smallint, 'saidas', 'Relatório do terminal (negativo)', 'saidas.relatorio-terminal-negativo', NULL, 0::smallint, 0::smallint))
                    AS s(role, kind, name, seed_key, map_key, monthly_role, settlement_rule) LOOP
                    cat_id := (cats->>spec.kind)::uuid;
                    mapped := CASE WHEN spec.map_key IS NULL THEN NULL ELSE (entry->>spec.map_key)::uuid END;
                    SELECT t."Id" INTO type_id FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId"
                        WHERE c."BranchId" = b AND t."SystemRole" = spec.role LIMIT 1;
                    IF type_id IS NOT NULL THEN
                        IF mapped IS NOT NULL AND mapped <> type_id THEN
                            RAISE EXCEPTION 'M96_PREFLIGHT: % names a type other than the branch''s existing system type (branch %)', spec.map_key, b;
                        END IF;
                        branch_id := b; item := 'type:' || spec.seed_key; outcome := 'present'; RETURN NEXT;
                        CONTINUE;
                    END IF;
                    IF mapped IS NOT NULL THEN
                        SELECT t.* INTO existing FROM "TransactionTypes" t WHERE t."Id" = mapped AND t."CategoryId" = cat_id;
                        IF NOT FOUND OR NOT existing."Active" OR existing."SystemRole" IS NOT NULL
                            OR existing."FiadoPaymentKind" IS NOT NULL OR existing."SettlementInstrument" <> 0
                            OR existing."RequiresTabAccountAndClient"
                            OR existing."MonthlyFinancialRole" NOT IN (0, spec.monthly_role) THEN
                            RAISE EXCEPTION 'M96_PREFLIGHT: % must be an active ordinary type of the mapped % category without classification (branch %)',
                                spec.map_key, spec.kind, b;
                        END IF;
                        IF existing."Name" <> spec.name AND EXISTS (SELECT 1 FROM "TransactionTypes" t
                            WHERE t."CategoryId" = cat_id AND t."Active" AND t."Name" = spec.name AND t."Id" <> mapped) THEN
                            RAISE EXCEPTION 'M96_PREFLIGHT: another active type is already named % (branch %)', spec.name, b;
                        END IF;
                        UPDATE "TransactionTypes"
                            SET "SystemRole" = spec.role, "MonthlyFinancialRole" = spec.monthly_role,
                                "SeedKey" = spec.seed_key, "Name" = spec.name
                            WHERE "Id" = mapped;
                        branch_id := b; item := 'type:' || spec.seed_key; outcome := 'converted'; RETURN NEXT;
                    ELSE
                        IF EXISTS (SELECT 1 FROM "TransactionTypes" t WHERE t."CategoryId" = cat_id AND t."Active" AND t."Name" = spec.name) THEN
                            RAISE EXCEPTION 'M96_PREFLIGHT: existing type % is not adopted; map its id or rename it (branch %)', spec.name, b;
                        END IF;
                        INSERT INTO "TransactionTypes" ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId",
                            "Active", "CreatedAt", "SystemRole", "SettlementInstrument", "MonthlyFinancialRole", "SeedKey")
                            VALUES (gen_random_uuid(), spec.name, spec.settlement_rule, false, cat_id, true, now(), spec.role, 0,
                                spec.monthly_role, spec.seed_key);
                        branch_id := b; item := 'type:' || spec.seed_key; outcome := 'created'; RETURN NEXT;
                    END IF;
                END LOOP;

                -- The cofre leg lives in its own system category, like the automatic fiado legs.
                IF NOT EXISTS (SELECT 1 FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId"
                    WHERE c."BranchId" = b AND t."SystemRole" = 8) THEN
                    safe_cat := gen_random_uuid();
                    safe_cat_name := 'Movimentos automáticos de cofre (entrada)';
                    IF EXISTS (SELECT 1 FROM "Categories" WHERE "BranchId" = b AND "Active" AND "Name" = safe_cat_name) THEN
                        safe_cat_name := safe_cat_name || ' — sistema ' || left(safe_cat::text, 8);
                    END IF;
                    INSERT INTO "Categories" ("Id", "Name", "DefaultDirection", "BranchId", "Active", "CreatedAt")
                        VALUES (safe_cat, safe_cat_name, 0, b, true, now());
                    INSERT INTO "TransactionTypes" ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId",
                        "Active", "CreatedAt", "SystemRole", "SettlementInstrument", "MonthlyFinancialRole", "SeedKey")
                        VALUES (gen_random_uuid(), 'Entrada no cofre — depósito do caixa', 0, false, safe_cat, true, now(), 8, 0, 0,
                            'sistema.cofre-deposito');
                    branch_id := b; item := 'type:sistema.cofre-deposito'; outcome := 'created'; RETURN NEXT;
                END IF;

                -- Ordinary seeds: repayment methods and the 32 Access expense types. Found by seed key; a same-name
                -- custom type is never adopted — that seed is skipped and reported.
                FOR spec IN SELECT * FROM (VALUES
                    ('entradas', 'Dinheiro', 'entradas.metodo-dinheiro', true, 1::smallint),
                    ('entradas', 'PIX', 'entradas.metodo-pix', true, 2::smallint),
                    ('administrativas', 'Água', 'administrativas.agua', false, NULL::smallint),
                    ('administrativas', 'Aluguel', 'administrativas.aluguel', false, NULL),
                    ('administrativas', 'Carro Forte', 'administrativas.carro-forte', false, NULL),
                    ('administrativas', 'Cel Fixo Internet', 'administrativas.cel-fixo-internet', false, NULL),
                    ('administrativas', 'Contabilidade', 'administrativas.contabilidade', false, NULL),
                    ('administrativas', 'Copa e Banheiro', 'administrativas.copa-banheiro', false, NULL),
                    ('administrativas', 'Energia', 'administrativas.energia', false, NULL),
                    ('administrativas', 'Escritório', 'administrativas.escritorio', false, NULL),
                    ('administrativas', 'Jurídico', 'administrativas.juridico', false, NULL),
                    ('administrativas', 'Marketing', 'administrativas.marketing', false, NULL),
                    ('administrativas', 'Seguro TFL', 'administrativas.seguro-tfl', false, NULL),
                    ('administrativas', 'Seguros', 'administrativas.seguros', false, NULL),
                    ('administrativas', 'Sindicato', 'administrativas.sindicato', false, NULL),
                    ('administrativas', 'Segurança', 'administrativas.seguranca', false, NULL),
                    ('administrativas', 'Outras Despesas', 'administrativas.outras-despesas', false, NULL),
                    ('pessoal', '13º salário', 'pessoal.decimo-terceiro', false, NULL),
                    ('pessoal', 'Comissão', 'pessoal.comissao', false, NULL),
                    ('pessoal', 'Férias', 'pessoal.ferias', false, NULL),
                    ('pessoal', 'FGTS', 'pessoal.fgts', false, NULL),
                    ('pessoal', 'Horas extras', 'pessoal.horas-extras', false, NULL),
                    ('pessoal', 'INSS', 'pessoal.inss', false, NULL),
                    ('pessoal', 'Pró Labore', 'pessoal.pro-labore', false, NULL),
                    ('pessoal', 'Salário mensal', 'pessoal.salario-mensal', false, NULL),
                    ('pessoal', 'Vale Transporte', 'pessoal.vale-transporte', false, NULL),
                    ('pessoal', 'Vale Alimentação', 'pessoal.vale-alimentacao', false, NULL),
                    ('pessoal', 'Outras despesas', 'pessoal.outras-despesas', false, NULL),
                    ('financeiras', 'ISSQN', 'financeiras.issqn', false, NULL),
                    ('financeiras', 'IPTU', 'financeiras.iptu', false, NULL),
                    ('financeiras', 'Simples Nacional', 'financeiras.simples-nacional', false, NULL),
                    ('financeiras', 'Taxas Bancárias', 'financeiras.taxas-bancarias', false, NULL),
                    ('financeiras', 'Juros', 'financeiras.juros', false, NULL),
                    ('financeiras', 'Outras despesas', 'financeiras.outras-despesas', false, NULL))
                    AS s(kind, name, seed_key, requires_tab, payment_kind) LOOP
                    cat_id := (cats->>spec.kind)::uuid;
                    IF EXISTS (SELECT 1 FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId"
                        WHERE c."BranchId" = b AND t."SeedKey" = spec.seed_key) THEN
                        branch_id := b; item := 'type:' || spec.seed_key; outcome := 'present'; RETURN NEXT;
                    ELSIF EXISTS (SELECT 1 FROM "TransactionTypes" t WHERE t."CategoryId" = cat_id AND t."Active" AND t."Name" = spec.name) THEN
                        branch_id := b; item := 'type:' || spec.seed_key; outcome := 'skipped-name-collision'; RETURN NEXT;
                    ELSE
                        INSERT INTO "TransactionTypes" ("Id", "Name", "SettlementRule", "RequiresTabAccountAndClient", "CategoryId",
                            "Active", "CreatedAt", "FiadoPaymentKind", "SettlementInstrument", "MonthlyFinancialRole", "SeedKey")
                            VALUES (gen_random_uuid(), spec.name, 0, spec.requires_tab, cat_id, true, now(), spec.payment_kind, 0, 0,
                                spec.seed_key);
                        branch_id := b; item := 'type:' || spec.seed_key; outcome := 'created'; RETURN NEXT;
                    END IF;
                END LOOP;

                -- Ordinary fee/report rows of open Ready sessions become the field's effect when unambiguous.
                SELECT s."LockDate"::date INTO lock_date FROM "Settings" s WHERE s."BranchId" = b AND s."Active" LIMIT 1;
                FOR session_row IN
                    SELECT d."Id", f.field, f.kind, f.roles
                    FROM "DailyCloses" d
                    CROSS JOIN (VALUES (1::smallint, 3::smallint, ARRAY[3, 4]), (2::smallint, 4::smallint, ARRAY[5]))
                        AS f(field, kind, roles)
                    WHERE d."BranchId" = b AND d."Active" AND d."MappingState" = 0
                      AND (lock_date IS NULL OR d."Date" > lock_date)
                      AND NOT EXISTS (SELECT 1 FROM "SessionFieldEffects" e WHERE e."SessionId" = d."Id" AND e."Field" = f.field)
                    ORDER BY d."Id", f.field
                LOOP
                    SELECT array_agg(t."Id" ORDER BY t."Id"), count(*) FILTER (WHERE t."Status" = 0)
                        INTO field_rows, draft_count
                    FROM "Transactions" t JOIN "TransactionTypes" tt ON tt."Id" = t."TransactionTypeId"
                    WHERE t."SessionId" = session_row."Id" AND t."Active" AND t."GenerationKind" IS NULL
                      AND t."Status" <> 2 AND tt."SystemRole" = ANY (session_row.roles);
                    IF field_rows IS NULL THEN
                        CONTINUE;
                    END IF;
                    IF array_length(field_rows, 1) = 1 AND draft_count = 0 THEN
                        SELECT t."Id", t."Value", tt."SystemRole" AS role INTO adopted
                        FROM "Transactions" t JOIN "TransactionTypes" tt ON tt."Id" = t."TransactionTypeId"
                        WHERE t."Id" = field_rows[1];
                        UPDATE "Transactions" SET "GenerationKind" = session_row.kind WHERE "Id" = adopted."Id";
                        slot_id := gen_random_uuid();
                        INSERT INTO "SessionFieldEffects" ("Id", "BranchId", "SessionId", "Field", "Value", "Revision",
                            "CurrentTransactionId", "UpdatedAt", "UpdatedByUserId", "Active", "CreatedAt")
                            VALUES (slot_id, b, session_row."Id", session_row.field,
                                CASE WHEN adopted.role = 4 THEN -adopted."Value" ELSE adopted."Value" END,
                                1, adopted."Id", NULL, NULL, true, now());
                        INSERT INTO "SessionFieldEffectRevisions" ("Id", "SessionFieldEffectId", "Revision", "PreviousValue",
                            "Value", "CreatedTransactionId", "CancelledTransactionId", "Origin", "ActorUserId", "RecordedAt",
                            "Active", "CreatedAt")
                            VALUES (gen_random_uuid(), slot_id, 1, 0,
                                CASE WHEN adopted.role = 4 THEN -adopted."Value" ELSE adopted."Value" END,
                                adopted."Id", NULL, 1, NULL, now(), true, now());
                        branch_id := b; item := 'session:' || session_row."Id" || ':field:' || session_row.field;
                        outcome := 'adopted'; RETURN NEXT;
                    ELSIF NOT EXISTS (SELECT 1 FROM "CustodyMappingIssues" i WHERE i."SourceKind" = 0
                        AND i."SourceId" = session_row."Id" AND i."IssueCode" = 2 AND i."Status" = 0) THEN
                        INSERT INTO "CustodyMappingIssues" ("Id", "BranchId", "SourceKind", "SourceId", "IssueCode",
                            "SourceDigest", "Status", "Active", "CreatedAt")
                            VALUES (gen_random_uuid(), b, 0, session_row."Id", 2,
                                encode(sha256(convert_to(array_to_string(field_rows, ','), 'UTF8')), 'hex'), 0, true, now());
                        branch_id := b; item := 'session:' || session_row."Id" || ':field:' || session_row.field;
                        outcome := 'ambiguous'; RETURN NEXT;
                    END IF;
                END LOOP;
            END LOOP;
        END $fn$;
        """;
}
