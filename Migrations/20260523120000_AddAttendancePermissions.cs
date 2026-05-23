using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    public partial class AddAttendancePermissions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('AttendancePermissions','U') IS NULL
                BEGIN
                    CREATE TABLE AttendancePermissions (
                        Id           INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        AttendanceId INT NOT NULL,
                        LeaveTime    time NOT NULL,
                        ReturnTime   time NULL,
                        Notes        nvarchar(max) NULL,
                        CreatedAt    datetime2 NOT NULL DEFAULT GETDATE(),
                        CONSTRAINT FK_AttendancePermissions_Attendances
                            FOREIGN KEY (AttendanceId) REFERENCES Attendances(Id) ON DELETE CASCADE
                    );
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('AttendancePermissions','U') IS NOT NULL
                    DROP TABLE AttendancePermissions;
            ");
        }
    }
}
