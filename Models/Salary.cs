using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Salary
    {
        public static class Statuses
        {
            public const string Pending = "معلق";
            public const string Paid = "مصروف";
            public const string SettledNoPayment = "تمت تسوية الراتب - لا يوجد مبلغ للصرف";
        }

        public int Id { get; set; }

        [Required]
        [Display(Name = "الموظف")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Display(Name = "الشهر")]
        public int Month { get; set; }

        [Display(Name = "السنة")]
        public int Year { get; set; }

        [Display(Name = "الراتب الأساسي")]
        [DataType(DataType.Currency)]
        public decimal BasicSalary { get; set; }

        [Display(Name = "البدلات")]
        [DataType(DataType.Currency)]
        public decimal Allowances { get; set; }

        [Display(Name = "الخصومات")]
        [DataType(DataType.Currency)]
        public decimal Deductions { get; set; }

        [Display(Name = "السلف المخصومة")]
        [DataType(DataType.Currency)]
        public decimal AdvanceDeducted { get; set; }

        [Display(Name = "دين على الموظف المخصوم")]
        [DataType(DataType.Currency)]
        public decimal EmployeeDebtDeducted { get; set; }

        // ─── تفصيل رصيد السلف وقت التسوية (لقطة تاريخية لا تتغيّر بعدها حتى لو تغيّر سجل السلف) ───

        [Display(Name = "السلف المرحلة من الأشهر السابقة")]
        [DataType(DataType.Currency)]
        public decimal CarriedAdvanceBalance { get; set; }

        [Display(Name = "السلف الجديدة خلال الشهر الحالي")]
        [DataType(DataType.Currency)]
        public decimal NewAdvancesAmount { get; set; }

        [Display(Name = "إجمالي رصيد السلف المستحق")]
        [DataType(DataType.Currency)]
        public decimal TotalAdvanceDue { get; set; }

        [Display(Name = "المبلغ المتاح لسداد السلف")]
        [DataType(DataType.Currency)]
        public decimal AvailableForAdvanceRepayment { get; set; }

        [Display(Name = "رصيد السلف المتبقي المرحّل للشهر القادم")]
        [DataType(DataType.Currency)]
        public decimal RemainingAdvanceCarried { get; set; }

        [Display(Name = "الصافي")]
        [DataType(DataType.Currency)]
        public decimal NetSalary { get; set; }

        [Display(Name = "تاريخ الصرف")]
        [DataType(DataType.Date)]
        public DateTime? PaidDate { get; set; }

        [Display(Name = "الحالة")]
        public string Status { get; set; } = "معلق";

        [Display(Name = "عمولة الفواتير")]
        [DataType(DataType.Currency)]
        public decimal CommissionAmount { get; set; }

        [Display(Name = "إجمالي الهدايا")]
        [DataType(DataType.Currency)]
        public decimal? GiftAmount { get; set; }

        [Display(Name = "هدية للموظف")]
        [DataType(DataType.Currency)]
        public decimal? HadiyaAmount { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "ملاحظة آلية")]
        public string? AutoNote { get; set; }

        [Display(Name = "طريقة الدفع")]
        public string PaymentMethod { get; set; } = "نقدي";

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public decimal TotalEntitlements => BasicSalary + CommissionAmount + Allowances + (GiftAmount ?? 0) + (HadiyaAmount ?? 0);

        [NotMapped]
        public bool IsSettledWithoutPayment => Status == Statuses.SettledNoPayment;
    }
}