namespace Salon.Models
{
    public class EmployeeEvaluationListViewModel
    {
        public List<EmployeeEvaluationRow> Rows { get; set; } = new();
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }
        public string? Department { get; set; }
    }

    public class EmployeeEvaluationRow
    {
        public Employee Employee { get; set; } = null!;
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }
        public int LeaveDays { get; set; }
        public int TotalAttendanceRecords { get; set; }
        public int PeriodDays { get; set; }
        public double AttendancePercent =>
            PeriodDays > 0 ? Math.Round(PresentDays * 100.0 / PeriodDays, 1) : 0;
        public decimal TotalSales { get; set; }
        public int TotalTransactions { get; set; }
        public decimal AverageSale => TotalTransactions > 0 ? Math.Round(TotalSales / TotalTransactions, 3) : 0;
    }
}