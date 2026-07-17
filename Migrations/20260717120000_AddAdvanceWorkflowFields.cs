using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddAdvanceWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManagerId",
                table: "EmployeeAdvances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManagerName",
                table: "EmployeeAdvances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DecisionAt",
                table: "EmployeeAdvances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "EmployeeAdvances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CashierId",
                table: "EmployeeAdvances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CashierName",
                table: "EmployeeAdvances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DisbursedAt",
                table: "EmployeeAdvances",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferReference",
                table: "EmployeeAdvances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferBank",
                table: "EmployeeAdvances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferNotes",
                table: "EmployeeAdvances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferredById",
                table: "EmployeeAdvances",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransferredByName",
                table: "EmployeeAdvances",
                type: "nvarchar(max)",
                nullable: true);

            // ترحيل الحالات القديمة إلى أسماء حالات مسار الموافقة/الصرف الجديد، حتى تبقى
            // السلف المسجَّلة قبل هذا التحديث متوافقة مع كل الاستعلامات والتقارير التي أصبحت
            // تعتمد على القيم الجديدة بدلاً من "معلق"/"موافق عليها" القديمة.
            migrationBuilder.Sql(
                "UPDATE EmployeeAdvances SET Status = N'بانتظار موافقة المدير' WHERE Status = N'معلق';");
            migrationBuilder.Sql(
                "UPDATE EmployeeAdvances SET Status = N'تم الصرف' WHERE Status = N'موافق عليها' AND PaymentMethod = N'نقدي';");
            migrationBuilder.Sql(
                "UPDATE EmployeeAdvances SET Status = N'تم التحويل' WHERE Status = N'موافق عليها' AND PaymentMethod <> N'نقدي';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE EmployeeAdvances SET Status = N'معلق' WHERE Status = N'بانتظار موافقة المدير';");
            migrationBuilder.Sql(
                "UPDATE EmployeeAdvances SET Status = N'موافق عليها' WHERE Status IN (N'تم الصرف', N'تم التحويل');");

            migrationBuilder.DropColumn(name: "ManagerId", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "ManagerName", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "DecisionAt", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "RejectionReason", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "CashierId", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "CashierName", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "DisbursedAt", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "TransferReference", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "TransferBank", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "TransferNotes", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "TransferredById", table: "EmployeeAdvances");
            migrationBuilder.DropColumn(name: "TransferredByName", table: "EmployeeAdvances");
        }
    }
}
