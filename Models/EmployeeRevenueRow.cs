namespace Salon.Models
{
    public class EmployeeRevenueRow
    {
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal TotalRevenue { get; set; }
        public decimal Cash { get; set; }
        public decimal Knet { get; set; }
        public decimal EmployeeDebt { get; set; }
        public decimal OwnerDebt { get; set; }
        public decimal BasicSalary { get; set; }
        public decimal CommissionRate { get; set; }
        public decimal SalesTarget { get; set; }
        public decimal CommissionAfterTargetRate { get; set; }
        public decimal CommissionBeforeTarget { get; set; }
        public decimal CommissionAfterTarget { get; set; }
        public decimal TotalCommission { get; set; }
        public decimal EmployeeServiceCommission { get; set; }
        public decimal Gifts { get; set; }
        public decimal Advances { get; set; }
        public decimal Deductions { get; set; }
        public decimal NetForEmployee { get; set; }
        public decimal NetForShop { get; set; }
        public int Count { get; set; }
        public string? DepartmentName { get; set; }
        public List<EmployeeServiceItem> Services { get; set; } = new();
        public List<EmployeeInvoiceItem> Invoices { get; set; } = new();
    }

    public class EmployeeServiceItem
    {
        public string ItemName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
    }

    public class EmployeeInvoiceItem
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? SaleType { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal Amount { get; set; }
    }
}