using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierInvoiceDepartment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Department",
                table: "SupplierInvoices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Department", table: "SupplierInvoices");
        }
    }
}
