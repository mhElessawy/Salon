using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerAssignedEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedEmployeeId",
                table: "Customers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Customers_AssignedEmployeeId",
                table: "Customers",
                column: "AssignedEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Customers_Employees_AssignedEmployeeId",
                table: "Customers",
                column: "AssignedEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Customers_Employees_AssignedEmployeeId",
                table: "Customers");

            migrationBuilder.DropIndex(
                name: "IX_Customers_AssignedEmployeeId",
                table: "Customers");

            migrationBuilder.DropColumn(
                name: "AssignedEmployeeId",
                table: "Customers");
        }
    }
}
