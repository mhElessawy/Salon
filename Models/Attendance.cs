using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Attendance
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "Employee")]
        public int EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Display(Name = "Date")]
        [DataType(DataType.Date)]
        public DateTime AttendanceDate { get; set; } = DateTime.Today;

        [Display(Name = "Check-in Time")]
        public TimeSpan? CheckIn { get; set; }

        [Display(Name = "Check-out Time")]
        public TimeSpan? CheckOut { get; set; }

        [Display(Name = "Status")]
        public string Status { get; set; } = "Present";

        [Display(Name = "Notes")]
        public string? Notes { get; set; }

        /// <summary>Role number in daily queue by Department</summary>
        [Display(Name = "Role")]
        public int? QueuePosition { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public List<AttendancePermission> Permissions { get; set; } = new();
    }
}