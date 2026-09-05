using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // كل صف هنا هو إيداع عهدة مستقل (تاريخ/وقت/مبلغ ثابت لا يتغير)، لكن "الرصيد المتاح" الفعلي
    // للموظف موحَّد على مستوى الموظف نفسه ويُجمع من كل إيداعاته المفتوحة معاً — انظر
    // Services/CustodyPoolCalculator. لهذا لا يوجد هنا أي مبلغ "محجوز" أو "متاح لطلب جديد" على
    // مستوى الصف نفسه؛ ده بقى مفهوم على مستوى الموظف بالكامل.
    public class Custody
    {
        public static class SettlementTypes
        {
            public const string RolledOver = "ترحيل";
            public const string Closed = "إقفال";
        }

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

        // null = إيداع نشط يُحسب ضمن رصيد الموظف الموحَّد. غير ذلك (RolledOver/Closed) = تمت
        // تسويته ولم يعد يُحسب ضمن الرصيد المتاح — انظر CustodyController.Settle
        public string? SettlementType { get; set; }

        public DateTime? SettledAt { get; set; }

        // true لإيداع أُنشئ تلقائياً كرصيد افتتاحي مرحَّل من تسوية سابقة (انظر CustodyController.Settle)
        public bool IsOpeningBalance { get; set; } = false;

        [NotMapped]
        public bool IsSettled => SettlementType != null;

        // توزيعات هذه العهدة على طلبات شراء مُعتمَدة (نقداً من العهدة) — لازم تُحمَّل (Include)
        // في أي استعلام يستخدم SpentAmount/RemainingAmount تحت. طلب شراء واحد ممكن يتوزَّع على
        // أكثر من عهدة لو مفيش عهدة واحدة كانت كافية لوحدها (انظر PurchaseRequestCustodyAllocation)
        public List<PurchaseRequestCustodyAllocation> Allocations { get; set; } = new();

        // دفعات فواتير موردين آجلة سُدِّدت من هذه العهدة — لازم تُحمَّل (Include) أيضاً في نفس الاستعلامات
        public List<SupplierInvoicePayment> InvoicePayments { get; set; } = new();

        // المبلغ المصروف فعلياً من هذا الإيداع تحديداً
        [NotMapped]
        public decimal SpentAmount => Allocations.Sum(a => a.Amount) + InvoicePayments.Sum(ip => ip.Amount);

        [NotMapped]
        public decimal RemainingAmount => Amount - SpentAmount;
    }
}
