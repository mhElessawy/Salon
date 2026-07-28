namespace Salon.Models
{
    // نموذج تعبئة نافذة "فاتورة مورد آجلة جديدة" المنبثقة (Popup) — يُستخدم لعرض الفورم
    // مبدئياً (GET) ولإعادة عرضه مع القيم المُدخلة عند فشل التحقق (POST)
    public class SupplierInvoiceCreateViewModel
    {
        public int SupplierId { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime InvoiceDate { get; set; } = DateTime.Today;
        public DateTime DueDate { get; set; } = DateTime.Today;
        public decimal DiscountAmount { get; set; }
        public decimal ExtraExpenses { get; set; }
        public string? Notes { get; set; }
        public bool IsDraft { get; set; }

        public string PaymentOption { get; set; } = "none";
        public decimal? PaymentAmount { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string PaymentSource { get; set; } = "الصندوق";
        public int? PaymentCustodyId { get; set; }
        public string? PaymentReference { get; set; }
        public string? PaymentNotes { get; set; }

        public List<SupplierInvoiceItemInput> Items { get; set; } = new();
        public List<SupplierInvoiceInstallmentInput> Installments { get; set; } = new();
    }

    public class SupplierInvoiceItemInput
    {
        public int? ProductId { get; set; }
        public string? ProductName { get; set; }
        public string? Unit { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
    }

    public class SupplierInvoiceInstallmentInput
    {
        public decimal Amount { get; set; }
        public DateTime? DueDate { get; set; }
    }
}
