using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M10OpeningCreditObservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClientCreditObservedAt",
                table: "GoLiveOpenings",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientCreditTransactionId",
                table: "GoLiveOpenings",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GoLiveOpenings_ClientCreditTransactionId",
                table: "GoLiveOpenings",
                column: "ClientCreditTransactionId");

            migrationBuilder.AddForeignKey(
                name: "FK_GoLiveOpenings_Transactions_ClientCreditTransactionId",
                table: "GoLiveOpenings",
                column: "ClientCreditTransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
                CREATE FUNCTION go_live_opening_source_guard() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF ROW(NEW."BranchId",NEW."AccountId",NEW."ClientId",NEW."IsCheque",NEW."Identity",NEW."SourceReference",NEW."SourceEvidence",NEW."AsOfDate",NEW."RecordedByUserId",NEW."PreviousId")
                           IS DISTINCT FROM ROW(OLD."BranchId",OLD."AccountId",OLD."ClientId",OLD."IsCheque",OLD."Identity",OLD."SourceReference",OLD."SourceEvidence",OLD."AsOfDate",OLD."RecordedByUserId",OLD."PreviousId")
                           OR (NOT OLD."Active" AND NEW."Active")
                           OR (OLD."ClientCreditObservedAt" IS NOT NULL AND ROW(NEW."ClientCreditObservedAt", NEW."ClientCreditTransactionId") IS DISTINCT FROM ROW(OLD."ClientCreditObservedAt", OLD."ClientCreditTransactionId")) THEN
                            RAISE EXCEPTION 'opening evidence is immutable' USING ERRCODE='23514', CONSTRAINT='CK_GoLive_OpeningImmutable';
                        END IF;
                    END IF;
                    IF NOT EXISTS (SELECT 1 FROM "Accounts" a WHERE a."Id"=NEW."AccountId" AND a."BranchId"=NEW."BranchId" AND a."Type"=CASE WHEN NEW."IsCheque" THEN 3 ELSE 2 END)
                       OR (NOT NEW."IsCheque" AND NEW."ClientId" IS NULL)
                       OR (NEW."ClientId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "Clients" c WHERE c."Id"=NEW."ClientId" AND c."BranchId"=NEW."BranchId"))
                       OR (NEW."PreviousId" IS NOT NULL AND NOT EXISTS (SELECT 1 FROM "GoLiveOpenings" p WHERE p."Id"=NEW."PreviousId" AND p."BranchId"=NEW."BranchId" AND p."Identity"=NEW."Identity" AND NOT p."Active")) THEN
                        RAISE EXCEPTION 'invalid opening source scope' USING ERRCODE='23514', CONSTRAINT='CK_GoLive_OpeningScope';
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_GoLiveOpenings_SourceGuard" BEFORE INSERT OR UPDATE ON "GoLiveOpenings"
                    FOR EACH ROW EXECUTE FUNCTION go_live_opening_source_guard();

                CREATE FUNCTION go_live_observe_client_credit() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN
                    IF NEW."Direction"=0 AND NEW."Active" AND NEW."Status"=1 AND EXISTS (SELECT 1 FROM "Accounts" a WHERE a."Id"=NEW."AccountId" AND a."Type"=2) THEN
                        UPDATE "GoLiveOpenings" SET "ClientCreditObservedAt"=now(), "ClientCreditTransactionId"=NEW."Id"
                        WHERE "BranchId"=NEW."BranchId" AND "ClientId"=NEW."ClientId" AND NOT "IsCheque" AND "ClientCreditObservedAt" IS NULL;
                    END IF;
                    IF TG_OP='UPDATE' AND OLD."Direction"=0 AND OLD."Active" AND OLD."Status"=1 AND EXISTS (SELECT 1 FROM "Accounts" a WHERE a."Id"=OLD."AccountId" AND a."Type"=2) THEN
                        UPDATE "GoLiveOpenings" SET "ClientCreditObservedAt"=now(), "ClientCreditTransactionId"=NEW."Id"
                        WHERE "BranchId"=OLD."BranchId" AND "ClientId"=OLD."ClientId" AND NOT "IsCheque" AND "ClientCreditObservedAt" IS NULL;
                    END IF;
                    RETURN NEW;
                END $$;
                CREATE TRIGGER "TR_Transactions_GoLiveCreditObservation" AFTER INSERT OR UPDATE OF "Status","Active","ClientId" ON "Transactions"
                    FOR EACH ROW EXECUTE FUNCTION go_live_observe_client_credit();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER "TR_Transactions_GoLiveCreditObservation" ON "Transactions";
                DROP FUNCTION go_live_observe_client_credit();
                DROP TRIGGER "TR_GoLiveOpenings_SourceGuard" ON "GoLiveOpenings";
                DROP FUNCTION go_live_opening_source_guard();
                """);
            migrationBuilder.DropForeignKey(
                name: "FK_GoLiveOpenings_Transactions_ClientCreditTransactionId",
                table: "GoLiveOpenings");

            migrationBuilder.DropIndex(
                name: "IX_GoLiveOpenings_ClientCreditTransactionId",
                table: "GoLiveOpenings");

            migrationBuilder.DropColumn(
                name: "ClientCreditObservedAt",
                table: "GoLiveOpenings");

            migrationBuilder.DropColumn(
                name: "ClientCreditTransactionId",
                table: "GoLiveOpenings");
        }
    }
}
