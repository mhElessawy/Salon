using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Custody
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "الموظف")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Display(Name = "المبلغ")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Display(Name = "تاريخ التسليم")]
        [DataType(DataType.Date)]
        public DateTime CustodyDate { get; set; } = DateTime.Today;

        // "نقدي" | "لينك" — العهدة لا تخصم من الصندوق بأي طريقة دفع، هي مجرد مبلغ منفصل تحت
        // عهدة الموظف يظهر معلوماتياً في تقارير الصندوق. لا تعتبر مصروفاً ولا تؤثر على الربح والخسارة.
        [Display(Name = "طريقة التسليم")]
        public string PaymentMethod { get; set; } = "نقدي";

        [Display(Name = "السبب / البيان")]
        public string? Reason { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // قديماً كانت كل عهدة تُنشئ مصروفاً مرتبطاً تلقائياً (فئة "عهدة") لخصمها من الصندوق —
        // أُلغي هذا الربط لأن العهدة لم تعد تؤثر على الصندوق، وبقي الحقل فقط للسجلات القديمة.
        public int? ExpenseId { get; set; }

        [ForeignKey("ExpenseId")]
        public Expense? Expense { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // طلبات الشراء المصروفة من هذه العهدة — لازم تُحمَّل (Include) في أي استعلام يستخدم الخصائص المحسوبة تحت
        public List<PurchaseRequest> PurchaseRequests { get; set; } = new();

        // المبلغ المصروف فعلياً — طلبات الشراء المعتمَدة (بعد مطابقة الكاشير) فقط
        [NotMapped]
        public decimal SpentAmount => PurchaseRequests
            .Where(p => p.Status == PurchaseRequest.Statuses.Completed)
            .Sum(p => p.ActualAmount ?? 0);

        // محجوز لطلبات شراء بانتظار الموافقة أو الشراء (تقديرياً، لم يُخصم بعد)
        [NotMapped]
        public decimal ReservedAmount => PurchaseRequests
            .Where(p => p.Status == PurchaseRequest.Statuses.Pending || p.Status == PurchaseRequest.Statuses.Approved)
            .Sum(p => p.EstimatedAmount);

        [NotMapped]
        public decimal RemainingAmount => Amount - SpentAmount;

        // الحد الأقصى المتاح لطلب شراء جديد (يستبعد المحجوز حتى لا يتجاوز مجموع الطلبات
        // المعتمدة مستقبلاً مبلغ العهدة الأصلي)
        [NotMapped]
        public decimal AvailableForRequest => RemainingAmount - ReservedAmount;
    }
}
