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

        // "نقدي" (يخصم من الصندوق) | "لينك" (لا يؤثر على الصندوق)
        [Display(Name = "طريقة التسليم")]
        public string PaymentMethod { get; set; } = "نقدي";

        [Display(Name = "السبب / البيان")]
        public string? Reason { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        // سجل المصروف المرتبط تلقائياً بهذه العهدة (فئة "عهدة") — يُنشأ عند التسليم ويُحذف معها
        public int? ExpenseId { get; set; }

        [ForeignKey("ExpenseId")]
        public Expense? Expense { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // تسويات/إرجاعات هذه العهدة — لازم تُحمَّل (Include) في أي استعلام يستخدم الخصائص المحسوبة تحت
        public List<CustodySettlement> Settlements { get; set; } = new();

        // المبلغ المسدَّد فعلياً — التسويات الموافَق عليها فقط (الطلبات المعلَّقة لا تخصم شيئاً بعد)
        [NotMapped]
        public decimal SettledAmount => Settlements.Where(s => s.Status == "موافق عليها").Sum(s => s.Amount);

        [NotMapped]
        public decimal PendingSettlementAmount => Settlements.Where(s => s.Status == "معلق").Sum(s => s.Amount);

        [NotMapped]
        public decimal RemainingAmount => Amount - SettledAmount;

        // الحد الأقصى المتاح لطلب تسوية جديد (يستبعد المتبقي + الطلبات المعلَّقة حتى لا يتجاوز مجموع
        // الطلبات الموافَق عليها مستقبلاً مبلغ العهدة الأصلي)
        [NotMapped]
        public decimal AvailableForSettlement => RemainingAmount - PendingSettlementAmount;

        [NotMapped]
        public string Status => RemainingAmount <= 0 ? "مسددة" : SettledAmount > 0 ? "مسددة جزئياً" : "مستلمة";
    }
}
