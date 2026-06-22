using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Salon.Models
{
    public class Customer
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        [Display(Name = "الاسم الكامل عربي")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Full Name English")]
        public string? FullNameEn { get; set; }

        [Display(Name = "القسم")]
        public string? Department { get; set; }

        [Display(Name = "رقم الهاتف")]
        public string? Phone { get; set; }

        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress]
        public string? Email { get; set; }

        [Display(Name = "تاريخ الميلاد")]
        [DataType(DataType.Date)]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "الجنس")]
        public string? Gender { get; set; }

        [Display(Name = "الملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "الموظف المسؤول")]
        public int? AssignedEmployeeId { get; set; }

        [ForeignKey("AssignedEmployeeId")]
        public Employee? AssignedEmployee { get; set; }

        [Display(Name = "تاريخ الإضافة")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsActive { get; set; } = true;

        public ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
        public ICollection<Sale> Sales { get; set; } = new List<Sale>();
        public ICollection<CustomerPackage> CustomerPackages { get; set; } = new List<CustomerPackage>();
    }
}