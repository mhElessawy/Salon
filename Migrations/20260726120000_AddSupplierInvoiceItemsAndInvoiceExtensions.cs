using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierInvoiceItemsAndInvoiceExtensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reference",
                table: "SupplierInvoices",
                type: "nvarchar(50)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "SupplierInvoices",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtraExpenses",
                table: "SupplierInvoices",
                type: "decimal(18,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentPath",
                table: "SupplierInvoices",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDraft",
                table: "SupplierInvoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsCancelled",
                table: "SupplierInvoices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "SupplierInvoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledByName",
                table: "SupplierInvoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelReason",
                table: "SupplierInvoices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttachmentPath",
                table: "SupplierInvoicePayments",
                type: "nvarchar(500)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierInvoiceItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierInvoiceId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(300)", nullable: false),
                    Unit = table.Column<string>(type: "nvarchar(50)", nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m),
                    Discount = table.Column<decimal>(type: "decimal(18,3)", nullable: false, defaultValue: 0m)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInvoiceItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInvoiceItems_SupplierInvoices_SupplierInvoiceId",
                        column: x => x.SupplierInvoiceId,
                        principalTable: "SupplierInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SupplierInvoiceItems_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceItems_SupplierInvoiceId",
                table: "SupplierInvoiceItems",
                column: "SupplierInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceItems_ProductId",
                table: "SupplierInvoiceItems",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "SupplierInvoiceItems");

            migrationBuilder.DropColumn(name: "AttachmentPath", table: "SupplierInvoicePayments");

            migrationBuilder.DropColumn(name: "Reference", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "DiscountAmount", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "ExtraExpenses", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "AttachmentPath", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "IsDraft", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "IsCancelled", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "CancelledAt", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "CancelledByName", table: "SupplierInvoices");
            migrationBuilder.DropColumn(name: "CancelReason", table: "SupplierInvoices");
        }
    }
}