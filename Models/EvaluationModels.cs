namespace Salon.Models
{
    public class EmployeeEvalSummary
    {
        public Employee Employee { get; set; } = null!;
        public int SalesCount { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalGifts { get; set; }
        public int AttendanceDays { get; set; }
        public decimal AvgPerSale => SalesCount > 0 ? Math.Round(TotalRevenue / SalesCount, 3) : 0;
    }
}
