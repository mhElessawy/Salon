using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    public partial class AddCustomerDepartmentAndEnName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Customers','FullNameEn') IS NULL
                    ALTER TABLE Customers ADD FullNameEn nvarchar(max) NULL;
            ");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('Customers','Department') IS NULL
                    ALTER TABLE Customers ADD Department nvarchar(max) NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Customers','FullNameEn') IS NOT NULL
                    ALTER TABLE Customers DROP COLUMN FullNameEn;
            ");

            migrationBuilder.Sql(@"
                IF COL_LENGTH('Customers','Department') IS NOT NULL
                    ALTER TABLE Customers DROP COLUMN Department;
            ");
        }
    }
}
