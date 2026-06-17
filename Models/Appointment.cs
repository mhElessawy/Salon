using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "العميل")]
        public int CustomerId { get; set; }

        [ForeignKey("CustomerId")]
        public Customer? Customer { get; set; }

        [Display(Name = "الموظف")]
        public int? EmployeeId { get; set; }

        [ForeignKey("EmployeeId")]
        public Employee? Employee { get; set; }

        [Required(ErrorMessage = "تاريخ الموعد مطلوب")]
        [Display(Name = "تاريخ الموعد")]
        [DataType(DataType.DateTime)]
        public DateTime AppointmentDate { get; set; }

        [Display(Name = "وقت الانتهاء")]
        public TimeSpan? EndTime { get; set; }

        [Display(Name = "الباقة المستخدمة")]
        public int? CustomerPackageId { get; set; }

        [ForeignKey("CustomerPackageId")]
        public CustomerPackage? CustomerPackage { get; set; }

        [Display(Name = "الحالة")]
        public string Status { get; set; } = "مجدول";

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();
    }

    public class AppointmentService
    {
        public int Id { get; set; }
        public int AppointmentId { get; set; }
        public Appointment? Appointment { get; set; }
        public int ServiceId { get; set; }
        public Service? Service { get; set; }
    }
}
