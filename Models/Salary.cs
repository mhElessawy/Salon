using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Salary
    {
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

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "طريقة الدفع")]
        public string PaymentMethod { get; set; } = "نقدي";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}