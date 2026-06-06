using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public class Deposit
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "Description is required")]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "Source")]
        public string? Source { get; set; }

        [Display(Name = "Deposit Date")]
        [DataType(DataType.Date)]
        public DateTime DepositDate { get; set; } = DateTime.Today;

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}