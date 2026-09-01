using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageAgreementSplitPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CashAmount",
                table: "PackageAgreements",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LinkAmount",
                table: "PackageAgreements",
                type: "decimal(18,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CashAmount",
                table: "PackageAgreements");

            migrationBuilder.DropColumn(
                name: "LinkAmount",
                table: "PackageAgreements");
        }
    }
}