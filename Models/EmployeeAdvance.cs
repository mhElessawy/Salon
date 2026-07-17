using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    // طلب سلفة موظف — يمر بمراحل:
    // بانتظار موافقة المدير -> (مرفوض | معتمد - بانتظار صرف الكاشير | معتمد - بانتظار التحويل البنكي)
    // -> (تم الصرف | تم التحويل) -> مسددة (بعد خصمها بالكامل من الراتب)
    // يمكن إلغاء الطلب فقط أثناء انتظار موافقة المدير.
    public class EmployeeAdvance
    {
        public static class Statuses
        {
            public const string PendingApproval = "بانتظار موافقة المدير";
            public const string Rejected = "مرفوض";
            public const string AwaitingCashierPayout = "معتمد - بانتظار صرف الكاشير";
            public const string AwaitingBankTransfer = "معتمد - بانتظار التحويل البنكي";
            public const string Disbursed = "تم الصرف";
            public const string Transferred = "تم التحويل";
            public const string Repaid = "مسددة";
            public const string Cancelled = "ملغي";

            // الحالات التي تعني أن مبلغ السلفة خرج فعلياً من الصندوق/البنك وسُجّل كدين على
            // الموظف — يُستخدم في حساب الصندوق والتقارير وأهلية الخصم من الراتب، بدلاً من
            // التحقق من "موافق عليها"/"مسددة" القديمة مباشرة.
            public static readonly string[] Realized = { Disbursed, Transferred, Repaid };
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

        [Display(Name = "تاريخ السلفة")]
        [DataType(DataType.Date)]
        public DateTime AdvanceDate { get; set; } = DateTime.Today;

        [Display(Name = "السبب")]
        public string? Reason { get; set; }

        [Display(Name = "طريقة الدفع")]
        public string PaymentMethod { get; set; } = "نقدي";

        [Display(Name = "الحالة")]
        public string Status { get; set; } = Statuses.PendingApproval;

        [Display(Name = "المبلغ المخصوم")]
        public decimal DeductedAmount { get; set; } = 0;

        // Alias for DeductedAmount - for backward compatibility
        [NotMapped]
        public decimal AmountPaid { get => DeductedAmount; set => DeductedAmount = value; }

        [Display(Name = "تاريخ السداد")]
        [DataType(DataType.Date)]
        public DateTime? PaidDate { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // بيانات موافقة/رفض المدير
        public string? ManagerId { get; set; }

        [Display(Name = "المدير")]
        public string? ManagerName { get; set; }

        [Display(Name = "تاريخ القرار")]
        public DateTime? DecisionAt { get; set; }

        [Display(Name = "سبب الرفض")]
        public string? RejectionReason { get; set; }

        // بيانات صرف الكاشير (الحالة النقدية)
        public string? CashierId { get; set; }

        [Display(Name = "الكاشير")]
        public string? CashierName { get; set; }

        [Display(Name = "تاريخ الصرف")]
        public DateTime? DisbursedAt { get; set; }

        // بيانات التحويل البنكي
        [Display(Name = "رقم مرجع التحويل")]
        public string? TransferReference { get; set; }

        [Display(Name = "البنك / الحساب المحوَّل منه")]
        public string? TransferBank { get; set; }

        [Display(Name = "ملاحظات التحويل")]
        public string? TransferNotes { get; set; }

        public string? TransferredById { get; set; }

        [Display(Name = "منفّذ التحويل")]
        public string? TransferredByName { get; set; }
    }
}
