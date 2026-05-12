namespace Salon.Models
{
    public class EmployeeEvaluationViewModel
    {
        public Employee? Employee { get; set; }
        public List<Employee> Employees { get; set; } = new();
        public DateTime DateFrom { get; set; }
        public DateTime DateTo { get; set; }

        // Attendance
        public int PresentDays { get; set; }
        public int AbsentDays { get; set; }
        public int LeaveDays { get; set; }
        public int TotalAttendanceRecords { get; set; }
        public double AttendancePercent =>
            TotalAttendanceRecords > 0 ? Math.Round(PresentDays * 100.0 / TotalAttendanceRecords, 1) : 0;

        // Sales
        public decimal TotalSales { get; set; }
        public int TotalTransactions { get; set; }
        public decimal AverageSale => TotalTransactions > 0 ? Math.Round(TotalSales / TotalTransactions, 3) : 0;
        public decimal HaircutSales { get; set; }
        public decimal MassageSales { get; set; }
        public decimal ProductSales { get; set; }
    }
}
