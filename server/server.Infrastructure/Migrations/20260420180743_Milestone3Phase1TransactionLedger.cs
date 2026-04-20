using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Milestone3Phase1TransactionLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionTypes_CategoryId",
                table: "TransactionTypes");

            migrationBuilder.AddColumn<bool>(
                name: "RequiresTabAccountAndClient",
                table: "TransactionTypes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "SettlementRule",
                table: "TransactionTypes",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "date", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TransactionTime = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    TransactionTypeId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Direction = table.Column<short>(type: "smallint", nullable: false),
                    AccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    DueDate = table.Column<DateTime>(type: "date", nullable: false),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OriginTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    RecordedByOperatorId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CancelledByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Transactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Transactions_Accounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "Accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "Branches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "Categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Clients_ClientId",
                        column: x => x.ClientId,
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Operators_RecordedByOperatorId",
                        column: x => x.RecordedByOperatorId,
                        principalTable: "Operators",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_TransactionTypes_TransactionTypeId",
                        column: x => x.TransactionTypeId,
                        principalTable: "TransactionTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Transactions_OriginTransactionId",
                        column: x => x.OriginTransactionId,
                        principalTable: "Transactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Users_CancelledByUserId",
                        column: x => x.CancelledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Transactions_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTypes_CategoryId_Name",
                table: "TransactionTypes",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_AccountId",
                table: "Transactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BranchId_AccountId_Direction_Date",
                table: "Transactions",
                columns: new[] { "BranchId", "AccountId", "Direction", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BranchId_Date_AccountId",
                table: "Transactions",
                columns: new[] { "BranchId", "Date", "AccountId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BranchId_DueDate",
                table: "Transactions",
                columns: new[] { "BranchId", "DueDate" },
                filter: "\"PaidAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BranchId_RecordedByOperatorId_Date",
                table: "Transactions",
                columns: new[] { "BranchId", "RecordedByOperatorId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BranchId_Status",
                table: "Transactions",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CancelledByUserId",
                table: "Transactions",
                column: "CancelledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CategoryId",
                table: "Transactions",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_ClientId",
                table: "Transactions",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CreatedByUserId",
                table: "Transactions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OriginTransactionId",
                table: "Transactions",
                column: "OriginTransactionId",
                filter: "\"OriginTransactionId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_RecordedByOperatorId",
                table: "Transactions",
                column: "RecordedByOperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TransactionTypeId",
                table: "Transactions",
                column: "TransactionTypeId");

            // One-time dev-stage data update: aligns pre-columns TransactionType rows with
            // the CreateBranchSeedFactory mapping. Not a production-grade backfill.
            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = true
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Cliente'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 1, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Depósito Dinheiro'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 3, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Cartão de Crédito'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'MarketPlace'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Sobra de Bolão'
                  AND c.""Name"" = 'Despesas Comerciais';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Sobra de Federal'
                  AND c.""Name"" = 'Despesas Comerciais';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 4, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Depósito Cheque'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'PIX'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 2, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Cartão de Débito'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Telesena'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Troca de Telesena'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Raspadinha'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Encalhe Federal'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = true
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Cliente'
                  AND c.""Name"" = 'Entradas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Pgto Prêmio'
                  AND c.""Name"" = 'Saídas';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Desconto'
                  AND c.""Name"" = 'Despesas Comerciais';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Volante rejeitado'
                  AND c.""Name"" = 'Despesas Comerciais';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Tarifa cartão'
                  AND c.""Name"" = 'Despesas Comerciais';
            ");

            migrationBuilder.Sql(@"
                UPDATE ""TransactionTypes"" tt
                SET ""SettlementRule"" = 0, ""RequiresTabAccountAndClient"" = false
                FROM ""Categories"" c
                WHERE tt.""CategoryId"" = c.""Id""
                  AND tt.""Name"" = 'Outras Despesas'
                  AND c.""Name"" = 'Despesas Comerciais';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_TransactionTypes_CategoryId_Name",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "RequiresTabAccountAndClient",
                table: "TransactionTypes");

            migrationBuilder.DropColumn(
                name: "SettlementRule",
                table: "TransactionTypes");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTypes_CategoryId",
                table: "TransactionTypes",
                column: "CategoryId");
        }
    }
}
