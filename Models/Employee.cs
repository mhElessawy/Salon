using System.ComponentModel.DataAnnotations;

namespace Salon.Models
{
    public enum ServiceType
    {
        Haircut,
        Massage,
        Both
    }

    public class Employee
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم مطلوب")]
        [Display(Name = "الاسم")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Display(Name = "رقم الهاتف")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "الجنسية مطلوبة")]
        [Display(Name = "الجنسية")]
        public string Nationality { get; set; } = string.Empty;

        [Required(ErrorMessage = "المسمى الوظيفي مطلوب")]
        [Display(Name = "المسمى الوظيفي")]
        public string JobTitle { get; set; } = string.Empty;

        [Required(ErrorMessage = "القسم مطلوب")]
        [Display(Name = "القسم")]
        public string Department { get; set; } = string.Empty;

        [Required(ErrorMessage = "الراتب مطلوب")]
        [Range(0, double.MaxValue, ErrorMessage = "الراتب يجب أن يكون أكبر من 0")]
        [Display(Name = "الراتب")]
        public decimal Salary { get; set; }

        [Required(ErrorMessage = "العمولة مطلوبة")]
        [Range(0, 100, ErrorMessage = "العمولة يجب أن تكون بين 0 و 100")]
        [Display(Name = "العمولة (%)")]
        public decimal Commission { get; set; }

        [Required(ErrorMessage = "تاريخ التعيين مطلوب")]
        [Display(Name = "تاريخ التعيين")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; }

        [Display(Name = "تاريخ انتهاء الإقامة")]
        [DataType(DataType.Date)]
        public DateTime? ResidencyExpiry { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Required(ErrorMessage = "نوع الخدمة مطلوب")]
        [Display(Name = "نوع الخدمة")]
        public ServiceType ServiceType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "القسم")]
        public int? DepartmentId { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("DepartmentId")]
        public Department? DepartmentRef { get; set; }

        public ICollection<Attendance>? Attendances { get; set; }
        public ICollection<Salary>? Salaries { get; set; }
        public ICollection<EmployeeAdvance>? Advances { get; set; }
    }
}