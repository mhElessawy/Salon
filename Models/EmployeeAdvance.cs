using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class EmployeeAdvance
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Required(ErrorMessage = "Amount is required")]
        [Display(Name = "Amount")]
        [DataType(DataType.Currency)]
        public decimal Amount { get; set; }

        [Display(Name = "Advance Date")]
        [DataType(DataType.Date)]
        public DateTime AdvanceDate { get; set; } = DateTime.Today;

        [Display(Name = "Reason")]
        public string? Reason { get; set; }

        [Display(Name = "Payment Method")]
        public string PaymentMethod { get; set; } = "Cash";

        [Display(Name = "Status")]
        public string Status { get; set; } = "Pending";

        [Display(Name = "Deducted Amount")]
        public decimal DeductedAmount { get; set; } = 0;

        // Alias for DeductedAmount - for backward compatibility
        [NotMapped]
        public decimal AmountPaid { get => DeductedAmount; set => DeductedAmount = value; }

        [Display(Name = "Payment Date")]
        [DataType(DataType.Date)]
        public DateTime? PaidDate { get; set; }

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}