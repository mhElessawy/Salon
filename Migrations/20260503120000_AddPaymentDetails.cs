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
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Sales','CashAmount') IS NULL
                    ALTER TABLE Sales ADD CashAmount decimal(18,3) NULL;

                IF COL_LENGTH('Sales','LinkAmount') IS NULL
                    ALTER TABLE Sales ADD LinkAmount decimal(18,3) NULL;

                IF COL_LENGTH('Sales','DebtEmployeeId') IS NULL
                    ALTER TABLE Sales ADD DebtEmployeeId int NULL;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sales_DebtEmployeeId' AND object_id = OBJECT_ID('Sales'))
                    CREATE INDEX IX_Sales_DebtEmployeeId ON Sales (DebtEmployeeId);

                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_Sales_Employees_DebtEmployeeId')
                    ALTER TABLE Sales ADD CONSTRAINT FK_Sales_Employees_DebtEmployeeId
                        FOREIGN KEY (DebtEmployeeId) REFERENCES Employees(Id) ON DELETE SET NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS WHERE CONSTRAINT_NAME = 'FK_Sales_Employees_DebtEmployeeId')
                    ALTER TABLE Sales DROP CONSTRAINT FK_Sales_Employees_DebtEmployeeId;

                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sales_DebtEmployeeId' AND object_id = OBJECT_ID('Sales'))
                    DROP INDEX IX_Sales_DebtEmployeeId ON Sales;

                IF COL_LENGTH('Sales','CashAmount') IS NOT NULL
                    ALTER TABLE Sales DROP COLUMN CashAmount;

                IF COL_LENGTH('Sales','LinkAmount') IS NOT NULL
                    ALTER TABLE Sales DROP COLUMN LinkAmount;

                IF COL_LENGTH('Sales','DebtEmployeeId') IS NOT NULL
                    ALTER TABLE Sales DROP COLUMN DebtEmployeeId;
            ");
        }
    }
}