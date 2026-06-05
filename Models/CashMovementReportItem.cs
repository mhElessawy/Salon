namespace Salon.Models
{
    public class CashMovementReportItem
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = "";       // "مصروف" | "إيداع"
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public string? Category { get; set; }        // فئة المصروف أو مصدر الإيداع
        public string? Notes { get; set; }
    }
}
