namespace Salon.Models
{
    public class DailyRevenueRow
    {
        public DateTime Date { get; set; }
        public decimal Total { get; set; }
        public decimal Cash { get; set; }
        public decimal Knet { get; set; }
        public decimal EmployeeDebt { get; set; }
        public decimal OwnerDebt { get; set; }
        public int Count { get; set; }
    }
}