using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Salon.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyClosureApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ApprovalStatus",
                table: "Shifts",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "مفتوح");

            migrationBuilder.AddColumn<decimal>(
                name: "ExpectedCashBalance",
                table: "Shifts",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CashDifferenceReason",
                table: "Shifts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "SystemKnetTotal",
                table: "Shifts",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeviceKnetTotal",
                table: "Shifts",
                type: "decimal(18,3)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KnetSettlementNumber",
                table: "Shifts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "KnetDifferenceReason",
                table: "Shifts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewedRevenue",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewedCash",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewedKnet",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewedExpenses",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewedWithdrawals",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewedDeposits",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewedAdvances",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewedEmployeeDebts",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ReviewedCustody",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "ConfirmedNoDiscrepancies",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ApprovalNotes",
                table: "Shifts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserId",
                table: "Shifts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ApprovedByUserName",
                table: "Shifts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "Shifts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAutoClosed",
                table: "Shifts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "AutoClosedAt",
                table: "Shifts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReopenedByUserId",
                table: "Shifts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReopenedByUserName",
                table: "Shifts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReopenedAt",
                table: "Shifts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReopenReason",
                table: "Shifts",
                type: "nvarchar(max)",
                nullable: true);

            // اليومية "المغلقة" حالياً (النظام القديم) تُعتبر معتمدة تلقائياً حتى لا تُقفل
            // كل بيانات الماضي فجأة بميزة القفل الجديدة.
            migrationBuilder.Sql("UPDATE Shifts SET ApprovalStatus = 'معتمد' WHERE Status = 'مغلق'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ApprovalStatus", table: "Shifts");
            migrationBuilder.DropColumn(name: "ExpectedCashBalance", table: "Shifts");
            migrationBuilder.DropColumn(name: "CashDifferenceReason", table: "Shifts");
            migrationBuilder.DropColumn(name: "SystemKnetTotal", table: "Shifts");
            migrationBuilder.DropColumn(name: "DeviceKnetTotal", table: "Shifts");
            migrationBuilder.DropColumn(name: "KnetSettlementNumber", table: "Shifts");
            migrationBuilder.DropColumn(name: "KnetDifferenceReason", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReviewedRevenue", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReviewedCash", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReviewedKnet", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReviewedExpenses", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReviewedWithdrawals", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReviewedDeposits", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReviewedAdvances", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReviewedEmployeeDebts", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReviewedCustody", table: "Shifts");
            migrationBuilder.DropColumn(name: "ConfirmedNoDiscrepancies", table: "Shifts");
            migrationBuilder.DropColumn(name: "ApprovalNotes", table: "Shifts");
            migrationBuilder.DropColumn(name: "ApprovedByUserId", table: "Shifts");
            migrationBuilder.DropColumn(name: "ApprovedByUserName", table: "Shifts");
            migrationBuilder.DropColumn(name: "ApprovedAt", table: "Shifts");
            migrationBuilder.DropColumn(name: "IsAutoClosed", table: "Shifts");
            migrationBuilder.DropColumn(name: "AutoClosedAt", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReopenedByUserId", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReopenedByUserName", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReopenedAt", table: "Shifts");
            migrationBuilder.DropColumn(name: "ReopenReason", table: "Shifts");
        }
    }
}
