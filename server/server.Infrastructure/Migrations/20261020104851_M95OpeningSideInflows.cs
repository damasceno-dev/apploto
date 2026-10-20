using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations;

/// <inheritdoc />
public partial class M95OpeningSideInflows : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Old seeds have random IDs and mutable names: only an explicitly reviewed ID map
        // can distinguish them from custom objects. Empty databases need no map.
        // This is a data-only migration; no API/report classification is introduced here.
        migrationBuilder.Sql("""
            DO $migration$
            DECLARE
                mapping jsonb := COALESCE(NULLIF(current_setting('loto.m95_seed_map', true), ''), '[]')::jsonb;
                entry jsonb;
                branch_id uuid;
                product_id uuid;
                category_id uuid;
                category_name text;
                type_name text;
                kind text;
            BEGIN
                LOCK TABLE "Branches", "Products", "Categories", "TransactionTypes", "DailyCloses",
                    "DailyCloseItems", "Settings" IN SHARE ROW EXCLUSIVE MODE;
                IF jsonb_typeof(mapping) <> 'array' THEN
                    RAISE EXCEPTION 'M95_PREFLIGHT: loto.m95_seed_map must be a reviewed JSON array';
                END IF;
                IF (SELECT count(*) FROM jsonb_array_elements(mapping)) <> (SELECT count(*) FROM "Branches")
                    OR EXISTS (SELECT 1 FROM jsonb_array_elements(mapping) e
                        GROUP BY e->>'branchId' HAVING count(*) <> 1)
                    OR EXISTS (SELECT 1 FROM jsonb_array_elements(mapping) e
                        WHERE NOT EXISTS (SELECT 1 FROM "Branches" b WHERE b."Id" = (e->>'branchId')::uuid)) THEN
                    RAISE EXCEPTION 'M95_PREFLIGHT: supply exactly one reviewed identity map per branch; see database-environments.md';
                END IF;
                FOR entry IN SELECT value FROM jsonb_array_elements(mapping) LOOP
                    branch_id := (entry->>'branchId')::uuid;
                    IF NOT (entry ? 'productId' AND entry ? 'receitaCategoryId' AND entry ? 'entradasCategoryId') THEN
                        RAISE EXCEPTION 'M95_PREFLIGHT: explicit product/category IDs or reviewed nulls required for branch %', branch_id;
                    END IF;
                    product_id := (entry->>'productId')::uuid;
                    IF product_id IS NOT NULL THEN
                        IF NOT EXISTS (SELECT 1 FROM "Products" WHERE "Id" = product_id AND "BranchId" = branch_id)
                            OR EXISTS (SELECT 1 FROM "Products" WHERE "Id" = product_id AND "Name" = 'Diferença Caixa') THEN
                            RAISE EXCEPTION 'M95_PREFLIGHT: invalid legacy fee product identity for branch %', branch_id;
                        END IF;
                        -- AA has no approved recovery policy. Never create a new retired-row
                        -- save/preview trap on an unlocked historical count, in any status.
                        IF EXISTS (SELECT 1 FROM "DailyCloseItems" i JOIN "DailyCloses" c ON c."Id" = i."DailyCloseId"
                            WHERE i."ProductId" = product_id AND i."Active" AND c."Active"
                            AND NOT EXISTS (SELECT 1 FROM "Settings" s WHERE s."BranchId" = branch_id
                                AND s."Active" AND c."Date" <= s."LockDate")) THEN
                            RAISE EXCEPTION 'M95_GAP_AA: unlocked counts retain the legacy fee product in branch %; migration blocked pending recovery policy', branch_id;
                        END IF;
                        UPDATE "Products" SET "Active" = FALSE WHERE "Id" = product_id AND "Active";
                    END IF;
                    FOREACH kind IN ARRAY ARRAY['receita', 'entradas'] LOOP
                        category_name := CASE kind WHEN 'receita' THEN 'Receita' ELSE 'Entradas' END;
                        type_name := COALESCE(entry->>(kind || 'TypeName'), CASE kind WHEN 'receita' THEN 'Tarifa Bolão' ELSE 'Operador' END);
                        IF length(btrim(type_name)) = 0 OR length(type_name) > 255 THEN
                            RAISE EXCEPTION 'M95_PREFLIGHT: invalid new type label for branch %', branch_id;
                        END IF;
                        category_id := (entry->>(kind || 'CategoryId'))::uuid;
                        IF category_id IS NULL THEN
                            IF EXISTS (SELECT 1 FROM "Categories" WHERE "BranchId" = branch_id AND "Name" = category_name AND "Active") THEN
                                RAISE EXCEPTION 'M95_PREFLIGHT: category name collision; review an explicit category ID for branch %', branch_id;
                            END IF;
                            category_id := gen_random_uuid();
                            INSERT INTO "Categories" ("Id", "Name", "DefaultDirection", "BranchId", "CreatedAt", "Active")
                                VALUES (category_id, category_name, 0, branch_id, now(), TRUE);
                        ELSIF NOT EXISTS (SELECT 1 FROM "Categories" WHERE "Id" = category_id AND "BranchId" = branch_id
                            AND "Active" AND "DefaultDirection" = 0)
                            OR EXISTS (SELECT 1 FROM "TransactionTypes" WHERE "CategoryId" = category_id AND "SystemRole" IS NOT NULL) THEN
                            RAISE EXCEPTION 'M95_PREFLIGHT: target category must be active, ordinary, In and in branch %', branch_id;
                        END IF;
                        IF EXISTS (SELECT 1 FROM "TransactionTypes" t JOIN "Categories" c ON c."Id" = t."CategoryId"
                            WHERE c."BranchId" = branch_id AND t."Name" = type_name) THEN
                            RAISE EXCEPTION 'M95_PREFLIGHT: existing type is not adopted; choose an unused reviewed label for branch %', branch_id;
                        END IF;
                        INSERT INTO "TransactionTypes" ("Id", "Name", "CategoryId", "SettlementRule", "RequiresTabAccountAndClient", "CreatedAt", "Active")
                            VALUES (gen_random_uuid(), type_name, category_id, 0, FALSE, now(), TRUE);
                    END LOOP;
                END LOOP;
            END $migration$;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // These user-editable types may already own financial rows. Never delete them or
        // reactivate a fee count slot and silently restore the old representation.
        migrationBuilder.Sql("""
            DO $$ BEGIN RAISE EXCEPTION 'M95_FORWARD_ONLY: restore a verified backup or apply a reviewed forward correction'; END $$;
            """);
    }
}
