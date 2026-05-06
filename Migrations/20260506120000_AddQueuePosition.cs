using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    public partial class AddQueuePosition : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Attendances','QueuePosition') IS NULL
                    ALTER TABLE Attendances ADD QueuePosition int NULL;
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Attendances','QueuePosition') IS NOT NULL
                    ALTER TABLE Attendances DROP COLUMN QueuePosition;
            ");
        }
    }
}
