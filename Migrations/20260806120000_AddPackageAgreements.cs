using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddPackageAgreements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PackageAgreements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerPackageId = table.Column<int>(type: "int", nullable: false),
                    CustomerId = table.Column<int>(type: "int", nullable: false),
                    ServicePackageId = table.Column<int>(type: "int", nullable: false),
                    SaleId = table.Column<int>(type: "int", nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CustomerPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PackageNameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PackageNameEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SessionCount = table.Column<int>(type: "int", nullable: false),
                    PackagePrice = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    AmountPaid = table.Column<decimal>(type: "decimal(18,3)", nullable: false),
                    PurchaseDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PaymentMethod = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TermsAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TermsEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignatureImage = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignedByUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignedByUserName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageAgreements_CustomerPackages_CustomerPackageId",
                        column: x => x.CustomerPackageId,
                        principalTable: "CustomerPackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageAgreements_Sales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "Sales",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    // Restrict (لا Cascade): Customer/ServicePackage ممكن الوصول لهم أيضاً بشكل
                    // غير مباشر عبر CustomerPackageId، فلو خليناها Cascade هيبقى فيه أكتر من
                    // مسار Cascade لنفس الجدول وSQL Server هيرفض إنشاء القيود
                    table.ForeignKey(
                        name: "FK_PackageAgreements_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PackageAgreements_ServicePackages_ServicePackageId",
                        column: x => x.ServicePackageId,
                        principalTable: "ServicePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PackageAgreements_CustomerPackageId",
                table: "PackageAgreements",
                column: "CustomerPackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageAgreements_CustomerId",
                table: "PackageAgreements",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageAgreements_ServicePackageId",
                table: "PackageAgreements",
                column: "ServicePackageId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageAgreements_SaleId",
                table: "PackageAgreements",
                column: "SaleId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PackageAgreements");
        }
    }
}
