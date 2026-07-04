using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class Deposit
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Display(Name = "المبلغ")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "البيان مطلوب")]
        [Display(Name = "البيان")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "المصدر")]
        public string? Source { get; set; }

        [Display(Name = "طريقة الدفع")]
        public string PaymentMethod { get; set; } = "نقدي";

        [Display(Name = "تاريخ الإيداع")]
        [DataType(DataType.Date)]
        public DateTime DepositDate { get; set; } = DateTime.Today;

        /// <summary>
        /// القسم: "حلاقة" | "مساج" | null (مشترك)
        /// </summary>
        [Required(ErrorMessage = "القسم مطلوب")]
        [Display(Name = "القسم")]
        public string Department { get; set; } = string.Empty;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}