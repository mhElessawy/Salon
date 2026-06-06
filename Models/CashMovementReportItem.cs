namespace Salon.Models
{
    public class CashMovementReportItem
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = "";       // "Expense" | "Deposit"
        public string Description { get; set; } = "";
        public decimal Amount { get; set; }
        public string? Category { get; set; }        // Expense category or deposit source
        public string? Notes { get; set; }
    }
}
