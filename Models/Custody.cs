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
    }
}
