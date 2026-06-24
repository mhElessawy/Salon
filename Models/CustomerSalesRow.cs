namespace Salon.Models
{
    public class CustomerSalesRow
    {
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public int InvoiceCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalCash { get; set; }
        public decimal TotalKnet { get; set; }
        public decimal TotalDebt { get; set; }
        public decimal TotalDiscount { get; set; }
        public DateTime? LastVisitDate { get; set; }
    }
}