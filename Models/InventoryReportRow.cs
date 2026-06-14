namespace Salon.Models
{
    public class InventoryReportRow
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Category { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SalePrice { get; set; }
        public int CurrentStock { get; set; }

        // Sales (from SaleItems linked to منتجات invoices)
        public int SoldQty { get; set; }
        public decimal SoldRevenue { get; set; }

        // Consumption (from StockMovement type=استهلاك)
        public int ConsumedQty { get; set; }
        public decimal ConsumedCost { get; set; }

        // Stock movements
        public int ReceivedQty { get; set; }
    }
}
