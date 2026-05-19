using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LookupFilteredActiveUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionTypes_CategoryId_Name",
                table: "TransactionTypes");

            migrationBuilder.DropIndex(
                name: "IX_Products_BranchId_Name",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_BranchId_Name",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTypes_CategoryId_Name",
                table: "TransactionTypes",
                columns: new[] { "CategoryId", "Name" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Products_BranchId_Name",
                table: "Products",
                columns: new[] { "BranchId", "Name" },
                unique: true,
                filter: "\"Active\" = true");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_BranchId_Name",
                table: "Categories",
                columns: new[] { "BranchId", "Name" },
                unique: true,
                filter: "\"Active\" = true");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TransactionTypes_CategoryId_Name",
                table: "TransactionTypes");

            migrationBuilder.DropIndex(
                name: "IX_Products_BranchId_Name",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Categories_BranchId_Name",
                table: "Categories");

            migrationBuilder.CreateIndex(
                name: "IX_TransactionTypes_CategoryId_Name",
                table: "TransactionTypes",
                columns: new[] { "CategoryId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_BranchId_Name",
                table: "Products",
                columns: new[] { "BranchId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Categories_BranchId_Name",
                table: "Categories",
                columns: new[] { "BranchId", "Name" },
                unique: true);
        }
    }
}
