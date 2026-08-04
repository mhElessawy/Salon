using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomerPackageWalletFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RegistrationType",
                table: "CustomerPackages",
                type: "nvarchar(50)",
                nullable: false,
                defaultValue: "بيع جديد");

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentBalance",
                table: "CustomerPackages",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                "UPDATE CustomerPackages SET CurrentBalance = PricePaid WHERE CurrentBalance = 0 AND PricePaid > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegistrationType",
                table: "CustomerPackages");

            migrationBuilder.DropColumn(
                name: "CurrentBalance",
                table: "CustomerPackages");
        }
    }
}