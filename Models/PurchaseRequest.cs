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

        // طريقة تمويل الشراء الفعلي — تُحدَّد وقت مطابقة الكاشير (وليس وقت الطلب)
        public static class PurchaseMethods
        {
            public const string Custody = "نقدًا من العهدة";
            public const string Deferred = "آجل من المورد";
        }

        public int Id { get; set; }

        [Required]
        [Display(Name = "الموظف")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

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
        [Display(Name = "طريقة الشراء")]
        public string PurchaseMethod { get; set; } = PurchaseMethods.Custody;

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

        // مصروف فعلي يُنشأ تلقائياً عند اعتماد وتسجيل الكاشير (شراء نقدي من العهدة فقط) — يمثل
        // الشراء الحقيقي المؤيَّد بفاتورة
        public int? ExpenseId { get; set; }

        [ForeignKey("ExpenseId")]
        public Expense? Expense { get; set; }

        // ذمة مورد آجلة تُنشأ تلقائياً عند اعتماد وتسجيل الكاشير باختيار طريقة الشراء "آجل من
        // المورد" — بديل عن ExpenseId، الاثنان لا يجتمعان أبداً في نفس الطلب
        public int? SupplierInvoiceId { get; set; }

        [ForeignKey("SupplierInvoiceId")]
        public SupplierInvoice? SupplierInvoice { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<PurchaseRequestItem> Items { get; set; } = new();

        // توزيع المبلغ الفعلي المصروف على عهدة/عهد الموظف — يُملأ فقط عند مطابقة الكاشير
        // واعتماد الطلب بطريقة الشراء "نقدًا من العهدة" (انظر PurchaseRequestCustodyAllocation)
        public List<PurchaseRequestCustodyAllocation> Allocations { get; set; } = new();

        [NotMapped]
        public string ItemsSummary => Items.Count == 0
            ? "-"
            : string.Join("، ", Items.Select(i => $"{i.ProductName} ({i.Quantity})"));
    }
}