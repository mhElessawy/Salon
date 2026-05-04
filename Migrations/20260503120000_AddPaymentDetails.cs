using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashAmount",
                table: "Sales",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LinkAmount",
                table: "Sales",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DebtEmployeeId",
                table: "Sales",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sales_DebtEmployeeId",
                table: "Sales",
                column: "DebtEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Employees_DebtEmployeeId",
                table: "Sales",
                column: "DebtEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Employees_DebtEmployeeId",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_DebtEmployeeId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "CashAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "LinkAmount",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "DebtEmployeeId",
                table: "Sales");
        }
    }
}
