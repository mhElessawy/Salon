using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class AttendancePermission
    {
        public int Id { get; set; }

        [Required]
        public int AttendanceId { get; set; }

        [ForeignKey("AttendanceId")]
        public Attendance? Attendance { get; set; }

        [Display(Name = "وقت الاستئذان")]
        public TimeSpan LeaveTime { get; set; }

        [Display(Name = "وقت العودة")]
        public TimeSpan? ReturnTime { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}