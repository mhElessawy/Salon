using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // ذمة/فاتورة مورد آجلة — المنتجات استُلمت لكن قيمتها لم تُدفع بعد، تُدفع على دفعة أو أكثر
    public class SupplierInvoice
    {
        public static class Statuses
        {
            public const string Draft = "مسودة";
            public const string ReturnedForEdit = "أُرجعت للتعديل";
            public const string Unpaid = "غير مدفوعة";
            public const string PartiallyPaid = "مدفوعة جزئيًا";
            public const string FullyPaid = "مدفوعة بالكامل";
            public const string Overdue = "متأخرة";
            public const string Cancelled = "ملغاة";
        }

        public int Id { get; set; }

        [Required]
        [Display(Name = "المورد")]
        public int SupplierId { get; set; }

        [ForeignKey("SupplierId")]
        public Supplier? Supplier { get; set; }

        // طلب الشراء الذي نشأت منه هذه الذمة (للتتبع فقط — قد تُنشأ الفاتورة يدوياً بدون طلب شراء لاحقاً)
        public int? PurchaseRequestId { get; set; }

        [ForeignKey("PurchaseRequestId")]
        public PurchaseRequest? PurchaseRequest { get; set; }

        [Required(ErrorMessage = "رقم الفاتورة مطلوب")]
        [Display(Name = "رقم الفاتورة")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Display(Name = "تاريخ الفاتورة")]
        [DataType(DataType.Date)]
        public DateTime InvoiceDate { get; set; } = DateTime.Today;

        [Required(ErrorMessage = "قيمة الفاتورة مطلوبة")]
        [Display(Name = "قيمة الفاتورة")]
        [DataType(DataType.Currency)]
        public decimal TotalAmount { get; set; }

        // مرجع داخلي يولّده النظام تلقائياً بعد الحفظ بصيغة SINV-000123
        [Display(Name = "المرجع الداخلي")]
        public string? Reference { get; set; }

        // خصم إجمالي على مستوى الفاتورة (بخلاف خصم كل سطر منتج)
        [Display(Name = "الخصم الإجمالي")]
        [DataType(DataType.Currency)]
        public decimal DiscountAmount { get; set; }

        // مصاريف إضافية على الفاتورة (شحن، جمارك، ...)
        [Display(Name = "مصاريف إضافية")]
        [DataType(DataType.Currency)]
        public decimal ExtraExpenses { get; set; }

        [Display(Name = "مرفق الفاتورة")]
        public string? AttachmentPath { get; set; }

        // مسودة: تُحفظ الفاتورة والمنتجات بدون أي أثر على المخزون أو الصندوق/البنك حتى تُعتمد
        [Display(Name = "مسودة")]
        public bool IsDraft { get; set; }

        [Display(Name = "ملغاة")]
        public bool IsCancelled { get; set; }

        [Display(Name = "تاريخ الإلغاء")]
        public DateTime? CancelledAt { get; set; }

        [Display(Name = "ألغاها")]
        public string? CancelledByName { get; set; }

        [Display(Name = "سبب الإلغاء")]
        public string? CancelReason { get; set; }

        // يُملأ عندما يُرجع المدير الفاتورة للتعديل بدلاً من اعتمادها — تبقى الفاتورة مسودة قابلة
        // للتعديل، ويُمسح هذا الحقل تلقائياً عند حفظ التعديل أو عند الاعتماد
        [Display(Name = "سبب الإرجاع للتعديل")]
        public string? ReturnReason { get; set; }

        [Display(Name = "تاريخ الإرجاع للتعديل")]
        public DateTime? ReturnedAt { get; set; }

        [Display(Name = "أرجعها للتعديل")]
        public string? ReturnedByName { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public string? CreatedByUserId { get; set; }

        [Display(Name = "أنشأها")]
        public string? CreatedByName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<SupplierInvoiceItem> Items { get; set; } = new();
        public List<SupplierInvoiceInstallment> Installments { get; set; } = new();
        public List<SupplierInvoicePayment> Payments { get; set; } = new();

        [NotMapped]
        public decimal ItemsSubtotal => Items.Sum(i => i.Quantity * i.UnitPrice);

        [NotMapped]
        public decimal ItemsDiscount => Items.Sum(i => i.Discount);

        [NotMapped]
        public decimal PaidAmount => Payments.Sum(p => p.Amount);

        [NotMapped]
        public decimal RemainingAmount => TotalAmount - PaidAmount;

        // أقرب موعد استحقاق لم يُغطَّ بعد بمجموع الدفعات المسجَّلة (وفق جدول الدفعات المخطَّط،
        // بالترتيب الزمني) — null لو مفيش دفعات مخطَّطة أو كلها مغطاة
        [NotMapped]
        public DateTime? NextDueDate
        {
            get
            {
                decimal covered = PaidAmount;
                decimal cumulative = 0;
                foreach (var inst in Installments.OrderBy(i => i.DueDate).ThenBy(i => i.Id))
                {
                    cumulative += inst.Amount;
                    if (cumulative > covered + 0.001m)
                        return inst.DueDate;
                }
                return null;
            }
        }

        [NotMapped]
        public bool IsOverdue
        {
            get
            {
                if (IsDraft || IsCancelled) return false;
                if (RemainingAmount <= 0.001m) return false;
                decimal covered = PaidAmount;
                decimal dueByToday = Installments
                    .Where(i => i.DueDate <= DateTime.Today)
                    .OrderBy(i => i.DueDate)
                    .Sum(i => i.Amount);
                return dueByToday > covered + 0.001m;
            }
        }

        // عدد أيام التأخير عن أقرب موعد استحقاق غير مغطَّى (لتقرير أعمار الديون) — 0 لو غير متأخرة
        [NotMapped]
        public int OverdueDays
        {
            get
            {
                if (!IsOverdue) return 0;
                decimal covered = PaidAmount;
                decimal cumulative = 0;
                foreach (var inst in Installments.OrderBy(i => i.DueDate).ThenBy(i => i.Id))
                {
                    cumulative += inst.Amount;
                    if (cumulative > covered + 0.001m)
                        return Math.Max(0, (DateTime.Today - inst.DueDate.Date).Days);
                }
                return 0;
            }
        }

        [NotMapped]
        public string Status
        {
            get
            {
                if (IsDraft) return !string.IsNullOrEmpty(ReturnReason) ? Statuses.ReturnedForEdit : Statuses.Draft;
                if (IsCancelled) return Statuses.Cancelled;
                if (RemainingAmount <= 0.001m) return Statuses.FullyPaid;
                if (IsOverdue) return Statuses.Overdue;
                if (PaidAmount > 0.001m) return Statuses.PartiallyPaid;
                return Statuses.Unpaid;
            }
        }
    }
}