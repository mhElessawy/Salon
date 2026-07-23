using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // ذمة/فاتورة مورد آجلة — المنتجات استُلمت لكن قيمتها لم تُدفع بعد، تُدفع على دفعة أو أكثر
    public class SupplierInvoice
    {
        public static class Statuses
        {
            public const string Unpaid = "غير مدفوعة";
            public const string PartiallyPaid = "مدفوعة جزئيًا";
            public const string FullyPaid = "مدفوعة بالكامل";
            public const string Overdue = "متأخرة";
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

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public string? CreatedByUserId { get; set; }

        [Display(Name = "أنشأها")]
        public string? CreatedByName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<SupplierInvoiceInstallment> Installments { get; set; } = new();
        public List<SupplierInvoicePayment> Payments { get; set; } = new();

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
                if (RemainingAmount <= 0.001m) return false;
                decimal covered = PaidAmount;
                decimal dueByToday = Installments
                    .Where(i => i.DueDate <= DateTime.Today)
                    .OrderBy(i => i.DueDate)
                    .Sum(i => i.Amount);
                return dueByToday > covered + 0.001m;
            }
        }

        [NotMapped]
        public string Status
        {
            get
            {
                if (RemainingAmount <= 0.001m) return Statuses.FullyPaid;
                if (IsOverdue) return Statuses.Overdue;
                if (PaidAmount > 0.001m) return Statuses.PartiallyPaid;
                return Statuses.Unpaid;
            }
        }
    }
}
