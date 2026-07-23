using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierInvoices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PurchaseMethod",
                table: "PurchaseRequests",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "نقدًا من العهدة");

            migrationBuilder.AddColumn<int>(
                name: "SupplierInvoiceId",
                table: "PurchaseRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SupplierInvoices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierId = table.Column<int>(type: "int", nullable: false),
                    PurchaseRequestId = table.Column<int>(type: "int", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvoiceDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInvoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInvoices_PurchaseRequests_PurchaseRequestId",
                        column: x => x.PurchaseRequestId,
                        principalTable: "PurchaseRequests",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierInvoices_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInvoiceInstallments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierInvoiceId = table.Column<int>(type: "int", nullable: false),
                    SequenceNo = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInvoiceInstallments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInvoiceInstallments_SupplierInvoices_SupplierInvoiceId",
                        column: x => x.SupplierInvoiceId,
                        principalTable: "SupplierInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SupplierInvoicePayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SupplierInvoiceId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustodyId = table.Column<int>(type: "int", nullable: true),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpenseId = table.Column<int>(type: "int", nullable: true),
                    PaidByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PaidByName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInvoicePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInvoicePayments_Custodies_CustodyId",
                        column: x => x.CustodyId,
                        principalTable: "Custodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SupplierInvoicePayments_Expenses_ExpenseId",
                        column: x => x.ExpenseId,
                        principalTable: "Expenses",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SupplierInvoicePayments_SupplierInvoices_SupplierInvoiceId",
                        column: x => x.SupplierInvoiceId,
                        principalTable: "SupplierInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseRequests_SupplierInvoiceId",
                table: "PurchaseRequests",
                column: "SupplierInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceInstallments_SupplierInvoiceId",
                table: "SupplierInvoiceInstallments",
                column: "SupplierInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoicePayments_CustodyId",
                table: "SupplierInvoicePayments",
                column: "CustodyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoicePayments_ExpenseId",
                table: "SupplierInvoicePayments",
                column: "ExpenseId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoicePayments_SupplierInvoiceId",
                table: "SupplierInvoicePayments",
                column: "SupplierInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_PurchaseRequestId",
                table: "SupplierInvoices",
                column: "PurchaseRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoices_SupplierId",
                table: "SupplierInvoices",
                column: "SupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseRequests_SupplierInvoices_SupplierInvoiceId",
                table: "PurchaseRequests",
                column: "SupplierInvoiceId",
                principalTable: "SupplierInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseRequests_SupplierInvoices_SupplierInvoiceId",
                table: "PurchaseRequests");

            migrationBuilder.DropTable(name: "SupplierInvoiceInstallments");
            migrationBuilder.DropTable(name: "SupplierInvoicePayments");
            migrationBuilder.DropTable(name: "SupplierInvoices");

            migrationBuilder.DropIndex(name: "IX_PurchaseRequests_SupplierInvoiceId", table: "PurchaseRequests");

            migrationBuilder.DropColumn(name: "SupplierInvoiceId", table: "PurchaseRequests");
            migrationBuilder.DropColumn(name: "PurchaseMethod", table: "PurchaseRequests");
        }
    }
}
