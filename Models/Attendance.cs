using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Attendance
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "الموظف")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Display(Name = "التاريخ")]
        [DataType(DataType.Date)]
        public DateTime AttendanceDate { get; set; } = DateTime.Today;

        [Display(Name = "وقت الحضور")]
        public TimeSpan? CheckIn { get; set; }

        [Display(Name = "وقت الانصراف")]
        public TimeSpan? CheckOut { get; set; }

        [Display(Name = "الحالة")]
        public string Status { get; set; } = "حاضر";

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
