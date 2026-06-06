using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Shift
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Shift name is required")]
        [Display(Name = "Shift Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Shift Date")]
        [DataType(DataType.Date)]
        public DateTime ShiftDate { get; set; } = DateTime.Today;

        [Display(Name = "Start Time")]
        public TimeSpan StartTime { get; set; }

        [Display(Name = "End Time")]
        public TimeSpan? EndTime { get; set; }

        [Display(Name = "Cashier")]
        public string? CashierName { get; set; }

        [Display(Name = "Opening Balance")]
        [DataType(DataType.Currency)]
        public decimal OpeningBalance { get; set; }

        [Display(Name = "Closing Balance")]
        [DataType(DataType.Currency)]
        public decimal? ClosingBalance { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Open";

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
