using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // طلب شراء يُصرف من عهدة الموظف — يمر بأربع مراحل:
    // بانتظار موافقة المدير -> (مرفوض | تمت الموافقة - بانتظار الشراء) -> معتمدة (بعد مطابقة الكاشير)
    public class PurchaseRequest
    {
        public static class Statuses
        {
            public const string Pending = "بانتظار موافقة المدير";
            public const string Rejected = "مرفوض";
            public const string Approved = "تمت الموافقة - بانتظار الشراء";
            public const string Completed = "معتمدة";
        }

        public int Id { get; set; }

        [Required]
        [Display(Name = "الموظف")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Required]
        [Display(Name = "العهدة")]
        public int CustodyId { get; set; }

        [ForeignKey("CustodyId")]
        public Custody? Custody { get; set; }

        [Display(Name = "تاريخ الطلب")]
        [DataType(DataType.Date)]
        public DateTime RequestDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "سبب الشراء مطلوب")]
        [Display(Name = "سبب الشراء")]
        public string Reason { get; set; } = string.Empty;

        [Display(Name = "المورد")]
        public int? SupplierId { get; set; }

        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        [Required(ErrorMessage = "القيمة التقديرية مطلوبة")]
        [Display(Name = "القيمة التقديرية")]
        [DataType(DataType.Currency)]
        public decimal EstimatedAmount { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "الحالة")]
        public string Status { get; set; } = Statuses.Pending;

        [Display(Name = "سبب الرفض")]
        public string? RejectionReason { get; set; }

        public string? ApprovedByUserId { get; set; }

        [Display(Name = "اعتمدها")]
        public string? ApprovedByName { get; set; }

        [Display(Name = "تاريخ الموافقة")]
        public DateTime? ApprovedAt { get; set; }

        // بيانات مطابقة واستلام الكاشير
        [Display(Name = "رقم الفاتورة")]
        public string? InvoiceNumber { get; set; }

        [Display(Name = "المبلغ الفعلي")]
        [DataType(DataType.Currency)]
        public decimal? ActualAmount { get; set; }

        [Display(Name = "صورة الفاتورة")]
        public string? InvoicePhotoPath { get; set; }

        public string? ReviewedByUserId { get; set; }

        [Display(Name = "راجعها الكاشير")]
        public string? ReviewedByName { get; set; }

        [Display(Name = "تاريخ المطابقة")]
        public DateTime? ReviewedAt { get; set; }

        // مصروف فعلي يُنشأ تلقائياً عند اعتماد وتسجيل الكاشير — يمثل الشراء الحقيقي المؤيَّد بفاتورة
        public int? ExpenseId { get; set; }

        [ForeignKey("ExpenseId")]
        public Expense? Expense { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<PurchaseRequestItem> Items { get; set; } = new();

        [NotMapped]
        public string ItemsSummary => Items.Count == 0
            ? "-"
            : string.Join("، ", Items.Select(i => $"{i.ProductName} ({i.Quantity})"));
    }
}
