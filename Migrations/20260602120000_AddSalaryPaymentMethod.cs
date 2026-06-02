using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddSalaryPaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentMethod",
                table: "Salaries",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "نقدي");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "Salaries");
        }
    }
}
