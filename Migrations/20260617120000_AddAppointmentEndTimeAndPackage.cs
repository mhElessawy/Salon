using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddAppointmentEndTimeAndPackage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "EndTime",
                table: "Appointments",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CustomerPackageId",
                table: "Appointments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_CustomerPackageId",
                table: "Appointments",
                column: "CustomerPackageId");

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_CustomerPackages_CustomerPackageId",
                table: "Appointments",
                column: "CustomerPackageId",
                principalTable: "CustomerPackages",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_CustomerPackages_CustomerPackageId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_CustomerPackageId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "CustomerPackageId",
                table: "Appointments");
        }
    }
}
