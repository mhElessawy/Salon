using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class Expense
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "البيان مطلوب")]
        [Display(Name = "البيان")]
        public string Description { get; set; } = string.Empty;

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Display(Name = "المبلغ")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Display(Name = "الفئة")]
        public string? Category { get; set; }

        /// <summary>
        /// القسم: "حلاقة" | "مساج" | null (مشترك للكل)
        /// </summary>
        [Display(Name = "القسم")]
        public string? Department { get; set; }

        [Display(Name = "تاريخ المصروف")]
        [DataType(DataType.Date)]
        public DateTime ExpenseDate { get; set; } = DateTime.Today;

        [Display(Name = "طريقة الدفع")]
        public string PaymentMethod { get; set; } = "نقدي";

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
