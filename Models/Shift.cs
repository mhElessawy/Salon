using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Shift
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الشفت مطلوب")]
        [Display(Name = "اسم الشفت")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "تاريخ الشفت")]
        [DataType(DataType.Date)]
        public DateTime ShiftDate { get; set; } = DateTime.Today;

        [Display(Name = "وقت البداية")]
        public TimeSpan StartTime { get; set; }

        [Display(Name = "وقت النهاية")]
        public TimeSpan? EndTime { get; set; }

        [Display(Name = "الكاشير")]
        public string? CashierName { get; set; }

        [Display(Name = "رصيد الفتح")]
        [DataType(DataType.Currency)]
        public decimal OpeningBalance { get; set; }

        [Display(Name = "رصيد الإغلاق")]
        [DataType(DataType.Currency)]
        public decimal? ClosingBalance { get; set; }

        [Display(Name = "الحالة")]
        public string Status { get; set; } = "مفتوح";

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
