using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddSalarySettlementBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CarriedAdvanceBalance",
                table: "Salaries",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "NewAdvancesAmount",
                table: "Salaries",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAdvanceDue",
                table: "Salaries",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AvailableForAdvanceRepayment",
                table: "Salaries",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "RemainingAdvanceCarried",
                table: "Salaries",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AutoNote",
                table: "Salaries",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CarriedAdvanceBalance",
                table: "Salaries");

            migrationBuilder.DropColumn(
                name: "NewAdvancesAmount",
                table: "Salaries");

            migrationBuilder.DropColumn(
                name: "TotalAdvanceDue",
                table: "Salaries");

            migrationBuilder.DropColumn(
                name: "AvailableForAdvanceRepayment",
                table: "Salaries");

            migrationBuilder.DropColumn(
                name: "RemainingAdvanceCarried",
                table: "Salaries");

            migrationBuilder.DropColumn(
                name: "AutoNote",
                table: "Salaries");
        }
    }
}
