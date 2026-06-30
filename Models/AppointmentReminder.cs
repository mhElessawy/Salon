using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class AppointmentReminder
    {
        public int Id { get; set; }

        [Required]
        public int AppointmentId { get; set; }

        [ForeignKey("AppointmentId")]
        public Appointment? Appointment { get; set; }

        /// <summary>كم دقيقة قبل الموعد يُرسل التنبيه</summary>
        [Required]
        public int MinutesBefore { get; set; }

        public bool IsSent { get; set; } = false;

        public DateTime? SentAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [NotMapped]
        public DateTime ScheduledAt => Appointment != null
            ? Appointment.AppointmentDate.AddMinutes(-MinutesBefore)
            : DateTime.MinValue;
    }
}
