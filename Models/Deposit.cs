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

        [Display(Name = "تاريخ الإيداع")]
        [DataType(DataType.Date)]
        public DateTime DepositDate { get; set; } = DateTime.Today;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
