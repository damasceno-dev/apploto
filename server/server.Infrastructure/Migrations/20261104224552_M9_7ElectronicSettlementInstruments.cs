using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M9_7ElectronicSettlementInstruments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "Instrument",
                table: "CardProviders",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Instrument",
                table: "CardProviders");
        }
    }
}
