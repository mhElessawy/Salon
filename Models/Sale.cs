using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Salon.Models
{
    public class Sale
    {
        public static class Statuses
        {
            public const string Completed = "مكتمل";
            public const string Cancelled = "ملغي";
            public const string Refunded = "مسترجع";
            public const string PartiallyRefunded = "مسترجع جزئياً";
        }

        public int Id { get; set; }

        [Display(Name = "رقم الفاتورة")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Display(Name = "العميل")]
        public int? CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [Display(Name = "الموظف")]
        public int? EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Display(Name = "تاريخ البيع")]
        [DataType(DataType.DateTime)]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        [Display(Name = "الإجمالي")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        [Display(Name = "الخصم")]
        [DataType(DataType.Currency)]
        public decimal Discount { get; set; }

        [Display(Name = "الصافي")]
        [DataType(DataType.Currency)]
        public decimal NetAmount { get; set; }

        [Display(Name = "طريقة الدفع")]
        public string PaymentMethod { get; set; } = "نقدي";

        [Display(Name = "نوع الفاتورة")]
        public string SaleType { get; set; } = "خدمة"; // "خدمة" أو "منتجات"

        [Display(Name = "الحالة")]
        public string Status { get; set; } = "مكتمل";

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "مبلغ الكاش")]
        public decimal? CashAmount { get; set; }

        [Display(Name = "مبلغ اللينك")]
        public decimal? LinkAmount { get; set; }

        [Display(Name = "رقم إيصال كي نت")]
        public string? KnetReceiptNumber { get; set; }

        [Display(Name = "موظف الدين")]
        public int? DebtEmployeeId { get; set; }

        [ForeignKey("DebtEmployeeId")]
        public Employee? DebtEmployee { get; set; }

        [Display(Name = "هدية الموظف")]
        [DataType(DataType.Currency)]
        public decimal? EmployeeGift { get; set; }

        [Display(Name = "هدية للموظف")]
        [DataType(DataType.Currency)]
        public decimal? GiftForEmployee { get; set; }

        [Display(Name = "المبلغ المدفوع نقداً")]
        [DataType(DataType.Currency)]
        public decimal? PaidAmount { get; set; }

        [Display(Name = "المبلغ المرتجع (الفكة)")]
        [DataType(DataType.Currency)]
        public decimal? ChangeAmount { get; set; }

        [Display(Name = "المستخدم")]
        public string? CreatedByUserId { get; set; }

        [Display(Name = "المستخدم")]
        public string? CreatedByUserName { get; set; }

        public ICollection<SaleItem> SaleItems { get; set; } = new List<SaleItem>();

        public ICollection<Refund> Refunds { get; set; } = new List<Refund>();

        [NotMapped]
        public decimal RefundedAmount => Refunds?.Sum(r => r.Amount) ?? 0;

        [NotMapped]
        public decimal RemainingRefundable => Math.Max(0, NetAmount - RefundedAmount);
    }

    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public Sale? Sale { get; set; }

        [Display(Name = "الخدمة / المنتج")]
        public int? ServiceId { get; set; }
        public Service? Service { get; set; }

        [Display(Name = "المنتج")]
        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        [Display(Name = "الاسم")]
        public string ItemName { get; set; } = string.Empty;

        [Display(Name = "الكمية")]
        public int Quantity { get; set; } = 1;

        [Display(Name = "السعر")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [Display(Name = "الإجمالي")]
        [DataType(DataType.Currency)]
        public decimal Total { get; set; }
    }
}