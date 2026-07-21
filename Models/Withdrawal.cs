using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class Withdrawal
    {
        public int Id { get; set; }

        [Display(Name = "القسم")]
        public string? Department { get; set; } // "حلاقة" | "مساج"

        [Required(ErrorMessage = "المبلغ مطلوب")]
        [Display(Name = "المبلغ")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "البيان مطلوب")]
        [Display(Name = "البيان")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "السبب")]
        public string? Reason { get; set; }

        [Display(Name = "طريقة السحب")]
        public string PaymentMethod { get; set; } = "نقدي";

        [Display(Name = "تاريخ السحب")]
        [DataType(DataType.Date)]
        public DateTime WithdrawalDate { get; set; } = DateTime.Today;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}